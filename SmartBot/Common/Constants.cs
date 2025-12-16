namespace SmartBot.Common;

/// <summary>
/// Application-wide constants to avoid magic numbers and strings
/// </summary>
public static class Constants
{
    /// <summary>
    /// Rate limiting configuration
    /// </summary>
    public static class RateLimits
    {
        /// <summary>
        /// Maximum requests per user per minute
        /// </summary>
        public const int MaxRequestsPerUserPerMinute = 20;

        /// <summary>
        /// Maximum requests per chat per minute
        /// </summary>
        public const int MaxRequestsPerChatPerMinute = 30;

        /// <summary>
        /// Telegram Bot API rate limit (messages per second)
        /// </summary>
        public const int TelegramApiMessagesPerSecond = 30;

        /// <summary>
        /// Burst allowance for short spikes
        /// </summary>
        public const int BurstSize = 10;
    }

    /// <summary>
    /// Cache configuration
    /// </summary>
    public static class Cache
    {
        /// <summary>
        /// Default cache expiration time
        /// </summary>
        public static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Long cache expiration for rarely changing data
        /// </summary>
        public static readonly TimeSpan LongExpiration = TimeSpan.FromHours(1);

        /// <summary>
        /// Short cache expiration for frequently changing data
        /// </summary>
        public static readonly TimeSpan ShortExpiration = TimeSpan.FromMinutes(1);

        // Cache Keys
        public const string AllAdvertisements = "cache_all_ads";
        public const string AllGroups = "cache_all_groups";
        public const string GroupSettingsPrefix = "cache_group_settings_";
        public const string StatisticsPrefix = "cache_stats_";
    }

    /// <summary>
    /// Retry policy configuration
    /// </summary>
    public static class Retry
    {
        /// <summary>
        /// Maximum number of retry attempts
        /// </summary>
        public const int MaxAttempts = 3;

        /// <summary>
        /// Initial delay before first retry
        /// </summary>
        public static readonly TimeSpan InitialDelay = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Maximum delay between retries
        /// </summary>
        public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Circuit breaker failure threshold
        /// </summary>
        public const int CircuitBreakerThreshold = 10;

        /// <summary>
        /// Circuit breaker break duration
        /// </summary>
        public static readonly TimeSpan CircuitBreakerDuration = TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Message validation limits
    /// </summary>
    public static class Validation
    {
        /// <summary>
        /// Maximum message length (Telegram limit)
        /// </summary>
        public const int MaxMessageLength = 4096;

        /// <summary>
        /// Maximum caption length (Telegram limit)
        /// </summary>
        public const int MaxCaptionLength = 1024;

        /// <summary>
        /// Minimum advertisement text length
        /// </summary>
        public const int MinAdTextLength = 1;

        /// <summary>
        /// Maximum advertisement text length
        /// </summary>
        public const int MaxAdTextLength = 2000;

        /// <summary>
        /// Maximum command length
        /// </summary>
        public const int MaxCommandLength = 100;
    }

    /// <summary>
    /// Logging configuration
    /// </summary>
    public static class Logging
    {
        /// <summary>
        /// Log file path
        /// </summary>
        public const string LogFilePath = "logs/smartbot-.log";

        /// <summary>
        /// Maximum log file size (10 MB)
        /// </summary>
        public const long MaxLogFileSizeMB = 10;

        /// <summary>
        /// Number of log files to retain
        /// </summary>
        public const int RetainedFileCountLimit = 7;
    }

    /// <summary>
    /// Database configuration
    /// </summary>
    public static class Database
    {
        /// <summary>
        /// Default database path
        /// </summary>
        public const string DefaultPath = "./data/smartbot.db";

        /// <summary>
        /// Database cache size (pages)
        /// </summary>
        public const int CacheSize = 10000;

        /// <summary>
        /// Backup interval
        /// </summary>
        public static readonly TimeSpan BackupInterval = TimeSpan.FromHours(24);
    }

    /// <summary>
    /// Background service configuration
    /// </summary>
    public static class BackgroundServices
    {
        /// <summary>
        /// Advertising check interval
        /// </summary>
        public static readonly TimeSpan AdCheckInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Health check interval
        /// </summary>
        public static readonly TimeSpan HealthCheckInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Metrics collection interval
        /// </summary>
        public static readonly TimeSpan MetricsInterval = TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Error messages
    /// </summary>
    public static class ErrorMessages
    {
        public const string BotTokenNotConfigured = "BOT_TOKEN environment variable yoki appsettings.json'da bot token sozlanmagan!";
        public const string AdminUserIdNotConfigured = "ADMIN_USER_ID sozlanmagan. Admin komandalar ishlamaydi.";
        public const string MessageTooLong = "Xabar juda uzun (max {0} belgi).";
        public const string InvalidCommand = "Noto'g'ri komanda formati.";
        public const string DatabaseError = "Ma'lumotlar bazasi xatosi.";
        public const string TelegramApiError = "Telegram API xatosi.";
        public const string RateLimitExceeded = "Juda ko'p so'rovlar. Iltimos, bir oz kuting.";
    }

    /// <summary>
    /// Success messages
    /// </summary>
    public static class SuccessMessages
    {
        public const string BotStarted = "Bot muvaffaqiyatli ishga tushdi: @{0}";
        public const string AdAdded = "Reklama qo'shildi!";
        public const string AdDeleted = "Reklama o'chirildi!";
        public const string AdToggled = "Reklama {0}!";
        public const string GroupAdded = "Bot guruhga qo'shildi: {0}";
        public const string GroupRemoved = "Bot guruhdan olib tashlandi: {0}";
    }
}
