using Microsoft.Extensions.Logging;
using SmartBot.Models;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace SmartBot.Services;

public interface IAdvertisingService
{
    Task SendAdsToGroupsAsync(ITelegramBotClient botClient, CancellationToken cancellationToken);
    bool ShouldSendAdToGroup(long chatId);
    Advertisement? GetNextAdForGroup(long chatId);
}

public class AdvertisingService : IAdvertisingService
{
    private readonly IDatabaseService _databaseService;
    private readonly AdvertisingConfig _config;
    private readonly ILogger<AdvertisingService> _logger;

    public AdvertisingService(
        IDatabaseService databaseService,
        AdvertisingConfig config,
        ILogger<AdvertisingService> logger)
    {
        _databaseService = databaseService;
        _config = config;
        _logger = logger;
    }

    public async Task SendAdsToGroupsAsync(ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("Advertising is disabled");
            return;
        }

        var groups = _databaseService.GetAllGroups();
        _logger.LogDebug("Checking {Count} groups for ad delivery", groups.Count);

        foreach (var group in groups)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                if (!ShouldSendAdToGroup(group.ChatId))
                    continue;

                var ad = GetNextAdForGroup(group.ChatId);
                if (ad == null)
                {
                    _logger.LogDebug("No active ads available for group {ChatId}", group.ChatId);
                    continue;
                }

                await SendAdToGroupAsync(botClient, group.ChatId, ad, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ads for group {ChatId}", group.ChatId);
            }
        }
    }

    public bool ShouldSendAdToGroup(long chatId)
    {
        try
        {
            var settings = _databaseService.GetGroupAdSettings(chatId);

            if (!settings.AdsEnabled)
                return false;

            if (settings.LastAdSentAt == null)
                return true;

            var intervalMinutes = settings.CustomIntervalMinutes ?? _config.DefaultIntervalMinutes;
            var nextAdTime = settings.LastAdSentAt.Value.AddMinutes(intervalMinutes);

            return DateTime.UtcNow >= nextAdTime;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if should send ad to {ChatId}", chatId);
            return false;
        }
    }

    public Advertisement? GetNextAdForGroup(long chatId)
    {
        try
        {
            var allAds = _databaseService.GetAllAdvertisements()
                .Where(a => a.IsActive)
                .OrderBy(a => a.DisplayOrder)
                .ToList();

            if (!allAds.Any())
                return null;

            var settings = _databaseService.GetGroupAdSettings(chatId);
            var lastIndex = settings.LastAdIndex;

            var nextIndex = (lastIndex + 1) % allAds.Count;
            return allAds[nextIndex];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting next ad for group {ChatId}", chatId);
            return null;
        }
    }

    private async Task SendAdToGroupAsync(
        ITelegramBotClient botClient,
        long chatId,
        Advertisement ad,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if ad has media
            if (!string.IsNullOrEmpty(ad.MediaType) && !string.IsNullOrEmpty(ad.MediaFileId))
            {
                // Send media with caption
                switch (ad.MediaType.ToLower())
                {
                    case "photo":
                        await botClient.SendPhotoAsync(
                            chatId,
                            new Telegram.Bot.Types.InputFileId(ad.MediaFileId),
                            caption: ad.Text,
                            cancellationToken: cancellationToken
                        );
                        break;

                    case "video":
                        await botClient.SendVideoAsync(
                            chatId,
                            new Telegram.Bot.Types.InputFileId(ad.MediaFileId),
                            caption: ad.Text,
                            cancellationToken: cancellationToken
                        );
                        break;

                    case "document":
                        await botClient.SendDocumentAsync(
                            chatId,
                            new Telegram.Bot.Types.InputFileId(ad.MediaFileId),
                            caption: ad.Text,
                            cancellationToken: cancellationToken
                        );
                        break;

                    default:
                        _logger.LogWarning("Unknown media type {MediaType} for ad {AdId}", ad.MediaType, ad.Id);
                        await botClient.SendTextMessageAsync(chatId, ad.Text, cancellationToken: cancellationToken);
                        break;
                }
            }
            else
            {
                // Send text only
                await botClient.SendTextMessageAsync(
                    chatId,
                    ad.Text,
                    cancellationToken: cancellationToken
                );
            }

            var allAds = _databaseService.GetAllAdvertisements()
                .Where(a => a.IsActive)
                .OrderBy(a => a.DisplayOrder)
                .ToList();
            var adIndex = allAds.FindIndex(a => a.Id == ad.Id);

            _databaseService.UpdateLastAdSent(chatId, adIndex);
            _databaseService.IncrementAdSent(chatId, ad.Id);

            _logger.LogInformation("Sent ad {AdId} (type: {MediaType}) to group {ChatId}", ad.Id, ad.MediaType ?? "text", chatId);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("chat not found"))
        {
            _logger.LogWarning("Chat {ChatId} not found, marking group as inactive", chatId);
            _databaseService.RemoveGroup(chatId);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("bot was blocked"))
        {
            _logger.LogWarning("Bot was blocked in chat {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending ad to group {ChatId}", chatId);
        }
    }
}
