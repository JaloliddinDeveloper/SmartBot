namespace SmartBot.Models;

public class GroupInfo
{
    public long Id { get; set; }
    public long ChatId { get; set; }
    public string? Title { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; }
}

public class UserJoinInfo
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long ChatId { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class BotStatistics
{
    public long Id { get; set; }
    public long ChatId { get; set; }
    public string? ChatTitle { get; set; }
    public int DeletedJoinMessages { get; set; }
    public int DeletedLeaveMessages { get; set; }
    public int DeletedSpamMessages { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class Advertisement
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int DisplayOrder { get; set; }
}

public class GroupAdSettings
{
    public long Id { get; set; }
    public long ChatId { get; set; }
    public bool AdsEnabled { get; set; }
    public int? CustomIntervalMinutes { get; set; }
    public DateTime? LastAdSentAt { get; set; }
    public int LastAdIndex { get; set; }
}

public class AdStatistics
{
    public long Id { get; set; }
    public long ChatId { get; set; }
    public int AdId { get; set; }
    public int TotalSent { get; set; }
    public DateTime LastSentAt { get; set; }
}
