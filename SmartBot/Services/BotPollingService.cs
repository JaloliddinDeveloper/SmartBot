using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace SmartBot.Services;

/// <summary>
/// Background service that handles Telegram bot polling for updates
/// </summary>
public class BotPollingService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly BotUpdateHandler _updateHandler;
    private readonly ILogger<BotPollingService> _logger;

    public BotPollingService(
        ITelegramBotClient botClient,
        BotUpdateHandler updateHandler,
        ILogger<BotPollingService> logger)
    {
        _botClient = botClient;
        _updateHandler = updateHandler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bot polling service started");

        // Configure receiving options
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[]
            {
                UpdateType.Message,
                UpdateType.MyChatMember,
                UpdateType.ChatMember
            },
            ThrowPendingUpdates = false
        };

        try
        {
            // Start receiving updates
            await _botClient.ReceiveAsync(
                _updateHandler,
                receiverOptions,
                stoppingToken
            );
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Bot polling service stopped gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in bot polling service");
            throw;
        }

        _logger.LogInformation("Bot polling service stopped");
    }
}
