using System.Text.RegularExpressions;

namespace SmartBot.Common;

/// <summary>
/// Input validation and sanitization utilities
/// </summary>
public static partial class InputValidator
{
    /// <summary>
    /// Validates and sanitizes message text
    /// </summary>
    /// <param name="text">Text to validate</param>
    /// <param name="maxLength">Maximum allowed length</param>
    /// <returns>Sanitized text</returns>
    /// <exception cref="ArgumentException">If validation fails</exception>
    public static string ValidateAndSanitizeMessage(string? text, int maxLength = Constants.Validation.MaxMessageLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Matn bo'sh bo'lishi mumkin emas.", nameof(text));

        // Remove control characters
        text = RemoveControlCharactersRegex().Replace(text, string.Empty);

        if (text.Length > maxLength)
            throw new ArgumentException(
                string.Format(Constants.ErrorMessages.MessageTooLong, maxLength),
                nameof(text));

        return text.Trim();
    }

    /// <summary>
    /// Validates advertisement text
    /// </summary>
    public static string ValidateAdText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Reklama matni bo'sh bo'lishi mumkin emas.", nameof(text));

        text = text.Trim();

        if (text.Length < Constants.Validation.MinAdTextLength)
            throw new ArgumentException(
                $"Reklama matni juda qisqa (minimum {Constants.Validation.MinAdTextLength} belgi).",
                nameof(text));

        if (text.Length > Constants.Validation.MaxAdTextLength)
            throw new ArgumentException(
                $"Reklama matni juda uzun (maksimum {Constants.Validation.MaxAdTextLength} belgi).",
                nameof(text));

        return text;
    }

    /// <summary>
    /// Validates command input
    /// </summary>
    public static string ValidateCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Komanda bo'sh bo'lishi mumkin emas.", nameof(command));

        command = command.Trim();

        if (command.Length > Constants.Validation.MaxCommandLength)
            throw new ArgumentException(
                $"Komanda juda uzun (maksimum {Constants.Validation.MaxCommandLength} belgi).",
                nameof(command));

        return command;
    }

    /// <summary>
    /// Validates chat ID
    /// </summary>
    public static long ValidateChatId(long chatId)
    {
        if (chatId == 0)
            throw new ArgumentException("Chat ID noto'g'ri.", nameof(chatId));

        return chatId;
    }

    /// <summary>
    /// Validates user ID
    /// </summary>
    public static long ValidateUserId(long userId)
    {
        if (userId == 0)
            throw new ArgumentException("User ID noto'g'ri.", nameof(userId));

        return userId;
    }

    /// <summary>
    /// Validates advertisement ID
    /// </summary>
    public static int ValidateAdId(int adId)
    {
        if (adId <= 0)
            throw new ArgumentException("Reklama ID noto'g'ri.", nameof(adId));

        return adId;
    }

    /// <summary>
    /// Validates interval in minutes
    /// </summary>
    public static int ValidateIntervalMinutes(int minutes, int min = 1, int max = 10080)
    {
        if (minutes < min || minutes > max)
            throw new ArgumentException(
                $"Interval noto'g'ri. {min} dan {max} gacha bo'lishi kerak.",
                nameof(minutes));

        return minutes;
    }

    /// <summary>
    /// Sanitizes text for logging (removes sensitive data)
    /// </summary>
    public static string SanitizeForLogging(string text, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Remove potential tokens/passwords
        text = TokenPatternRegex().Replace(text, "[TOKEN]");

        // Truncate if too long
        if (text.Length > maxLength)
            text = text[..maxLength] + "...";

        return text;
    }

    /// <summary>
    /// Checks if text contains spam patterns
    /// </summary>
    public static bool ContainsSuspiciousPatterns(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        // Check for excessive URLs
        var urlCount = UrlPatternRegex().Matches(text).Count;
        if (urlCount > 5)
            return true;

        // Check for excessive mentions
        var mentionCount = MentionPatternRegex().Matches(text).Count;
        if (mentionCount > 5)
            return true;

        // Check for excessive emojis (more than 50% of message)
        var emojiCount = EmojiPatternRegex().Matches(text).Count;
        if (text.Length > 0 && (emojiCount * 2 > text.Length))
            return true;

        return false;
    }

    // Source-generated regex patterns for better performance
    [GeneratedRegex(@"[\x00-\x1F\x7F]")]
    private static partial Regex RemoveControlCharactersRegex();

    [GeneratedRegex(@"\d+:[A-Za-z0-9_-]{35}")]
    private static partial Regex TokenPatternRegex();

    [GeneratedRegex(@"https?://[^\s]+")]
    private static partial Regex UrlPatternRegex();

    [GeneratedRegex(@"@\w+")]
    private static partial Regex MentionPatternRegex();

    // Simplified emoji pattern - detects common emoji ranges
    [GeneratedRegex(@"[\p{So}\p{Sk}\p{Sm}]")]
    private static partial Regex EmojiPatternRegex();
}
