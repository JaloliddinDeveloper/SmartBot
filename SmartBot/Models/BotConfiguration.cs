namespace SmartBot.Models;

public class BotConfiguration
{
    public string BotToken { get; set; } = string.Empty;
    public long AdminUserId { get; set; }
}

public class Features
{
    public bool AutoDeleteJoinLeaveMessages { get; set; }
    public bool SpamDetection { get; set; }
    public bool EnableStatistics { get; set; }
}

public class SpamDetectionConfig
{
    public List<string> Keywords { get; set; } = new();
    public int MaxUrlsPerMessage { get; set; }
    public bool BlockNewUsersWithUrls { get; set; }
    public int NewUserTimeWindowMinutes { get; set; }
}

public class AdvertisingConfig
{
    public bool Enabled { get; set; }
    public int DefaultIntervalMinutes { get; set; }
    public bool AutoStartOnBotStartup { get; set; }
}

public class AppSettings
{
    public BotConfiguration BotConfiguration { get; set; } = new();
    public Features Features { get; set; } = new();
    public SpamDetectionConfig SpamDetection { get; set; } = new();
    public AdvertisingConfig Advertising { get; set; } = new();
}
