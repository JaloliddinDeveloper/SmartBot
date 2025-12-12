using Microsoft.Extensions.Logging;
using SmartBot.Models;
using System.Text.RegularExpressions;

namespace SmartBot.Services;

public interface ISpamDetectionService
{
    bool IsSpam(string? messageText, long userId, long chatId);
}

public partial class SpamDetectionService : ISpamDetectionService
{
    private readonly SpamDetectionConfig _config;
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<SpamDetectionService> _logger;

    [GeneratedRegex(@"(https?://|t\.me/|@\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    public SpamDetectionService(
        SpamDetectionConfig config,
        IDatabaseService databaseService,
        ILogger<SpamDetectionService> logger)
    {
        _config = config;
        _databaseService = databaseService;
        _logger = logger;
    }

    public bool IsSpam(string? messageText, long userId, long chatId)
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return false;

        try
        {
            // Check for spam keywords
            if (ContainsSpamKeywords(messageText))
            {
                _logger.LogInformation("Spam detected (keywords) from user {UserId} in chat {ChatId}", userId, chatId);
                return true;
            }

            // Count URLs in message
            var urlCount = CountUrls(messageText);
            if (urlCount > _config.MaxUrlsPerMessage)
            {
                _logger.LogInformation("Spam detected (too many URLs: {Count}) from user {UserId} in chat {ChatId}",
                    urlCount, userId, chatId);
                return true;
            }

            // Check if new user is posting URLs
            if (_config.BlockNewUsersWithUrls && urlCount > 0)
            {
                if (_databaseService.IsNewUser(userId, chatId, _config.NewUserTimeWindowMinutes))
                {
                    _logger.LogInformation("Spam detected (new user with URLs) from user {UserId} in chat {ChatId}",
                        userId, chatId);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking spam for message from user {UserId} in chat {ChatId}", userId, chatId);
            return false;
        }
    }

    private bool ContainsSpamKeywords(string text)
    {
        var lowerText = text.ToLower();
        return _config.Keywords.Any(keyword => lowerText.Contains(keyword.ToLower()));
    }

    private int CountUrls(string text)
    {
        var matches = UrlRegex().Matches(text);
        return matches.Count;
    }
}
