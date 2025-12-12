using LiteDB;
using Microsoft.Extensions.Logging;
using SmartBot.Models;

namespace SmartBot.Services;

public interface IDatabaseService
{
    void AddOrUpdateGroup(long chatId, string title);
    void RemoveGroup(long chatId);
    List<GroupInfo> GetAllGroups();
    void TrackUserJoin(long userId, long chatId);
    bool IsNewUser(long userId, long chatId, int timeWindowMinutes);
    void IncrementDeletedJoinMessages(long chatId);
    void IncrementDeletedLeaveMessages(long chatId);
    void IncrementDeletedSpamMessages(long chatId);
    BotStatistics GetStatistics(long chatId);
    List<BotStatistics> GetAllStatistics();

    // Advertisement management
    void AddAdvertisement(string text);
    List<Advertisement> GetAllAdvertisements();
    Advertisement? GetAdvertisement(int id);
    void DeleteAdvertisement(int id);
    void ToggleAdvertisement(int id);

    // Group ad settings
    GroupAdSettings GetGroupAdSettings(long chatId);
    void UpdateGroupAdSettings(GroupAdSettings settings);
    void SetGroupAdInterval(long chatId, int? intervalMinutes);
    void UpdateLastAdSent(long chatId, int adIndex);

    // Ad statistics
    void IncrementAdSent(long chatId, int adId);
    List<AdStatistics> GetAdStatistics();
    List<AdStatistics> GetAdStatisticsForChat(long chatId);
}

public class DatabaseService : IDatabaseService, IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<GroupInfo> _groups;
    private readonly ILiteCollection<UserJoinInfo> _userJoins;
    private readonly ILiteCollection<BotStatistics> _statistics;
    private readonly ILiteCollection<Advertisement> _advertisements;
    private readonly ILiteCollection<GroupAdSettings> _groupAdSettings;
    private readonly ILiteCollection<AdStatistics> _adStatistics;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(ILogger<DatabaseService> logger)
    {
        _logger = logger;
        _db = new LiteDatabase("smartbot.db");
        _groups = _db.GetCollection<GroupInfo>("groups");
        _userJoins = _db.GetCollection<UserJoinInfo>("user_joins");
        _statistics = _db.GetCollection<BotStatistics>("statistics");
        _advertisements = _db.GetCollection<Advertisement>("advertisements");
        _groupAdSettings = _db.GetCollection<GroupAdSettings>("group_ad_settings");
        _adStatistics = _db.GetCollection<AdStatistics>("ad_statistics");

        // Create indexes
        _groups.EnsureIndex(x => x.ChatId);
        _userJoins.EnsureIndex(x => x.UserId);
        _userJoins.EnsureIndex(x => x.ChatId);
        _statistics.EnsureIndex(x => x.ChatId);
        _advertisements.EnsureIndex(x => x.Id);
        _advertisements.EnsureIndex(x => x.DisplayOrder);
        _groupAdSettings.EnsureIndex(x => x.ChatId);
        _adStatistics.EnsureIndex(x => x.ChatId);
        _adStatistics.EnsureIndex(x => x.AdId);
    }

    public void AddOrUpdateGroup(long chatId, string title)
    {
        try
        {
            var group = _groups.FindOne(g => g.ChatId == chatId);
            if (group == null)
            {
                group = new GroupInfo
                {
                    ChatId = chatId,
                    Title = title,
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true
                };
                _groups.Insert(group);
                _logger.LogInformation("Added new group: {Title} ({ChatId})", title, chatId);
            }
            else
            {
                group.Title = title;
                group.IsActive = true;
                _groups.Update(group);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding/updating group {ChatId}", chatId);
        }
    }

    public void RemoveGroup(long chatId)
    {
        try
        {
            var group = _groups.FindOne(g => g.ChatId == chatId);
            if (group != null)
            {
                group.IsActive = false;
                _groups.Update(group);
                _logger.LogInformation("Removed group: {ChatId}", chatId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing group {ChatId}", chatId);
        }
    }

    public List<GroupInfo> GetAllGroups()
    {
        return _groups.Query().Where(g => g.IsActive).ToList();
    }

    public void TrackUserJoin(long userId, long chatId)
    {
        try
        {
            var userJoin = new UserJoinInfo
            {
                UserId = userId,
                ChatId = chatId,
                JoinedAt = DateTime.UtcNow
            };
            _userJoins.Insert(userJoin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking user join {UserId} in {ChatId}", userId, chatId);
        }
    }

    public bool IsNewUser(long userId, long chatId, int timeWindowMinutes)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-timeWindowMinutes);
            var userJoin = _userJoins.Query()
                .Where(uj => uj.UserId == userId && uj.ChatId == chatId && uj.JoinedAt >= cutoffTime)
                .FirstOrDefault();

            return userJoin != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user is new {UserId} in {ChatId}", userId, chatId);
            return false;
        }
    }

    public void IncrementDeletedJoinMessages(long chatId)
    {
        IncrementStatistic(chatId, s => s.DeletedJoinMessages++);
    }

    public void IncrementDeletedLeaveMessages(long chatId)
    {
        IncrementStatistic(chatId, s => s.DeletedLeaveMessages++);
    }

    public void IncrementDeletedSpamMessages(long chatId)
    {
        IncrementStatistic(chatId, s => s.DeletedSpamMessages++);
    }

    private void IncrementStatistic(long chatId, Action<BotStatistics> incrementAction)
    {
        try
        {
            var stat = _statistics.FindOne(s => s.ChatId == chatId);
            if (stat == null)
            {
                stat = new BotStatistics
                {
                    ChatId = chatId,
                    LastUpdated = DateTime.UtcNow
                };
                incrementAction(stat);
                _statistics.Insert(stat);
            }
            else
            {
                incrementAction(stat);
                stat.LastUpdated = DateTime.UtcNow;
                _statistics.Update(stat);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing statistic for {ChatId}", chatId);
        }
    }

    public BotStatistics GetStatistics(long chatId)
    {
        return _statistics.FindOne(s => s.ChatId == chatId) ?? new BotStatistics { ChatId = chatId };
    }

    public List<BotStatistics> GetAllStatistics()
    {
        return _statistics.FindAll().ToList();
    }

    // Advertisement Management
    public void AddAdvertisement(string text)
    {
        try
        {
            var allAds = _advertisements.FindAll().ToList();
            var maxOrder = allAds.Any() ? allAds.Max(a => a.DisplayOrder) : 0;
            var ad = new Advertisement
            {
                Text = text,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                DisplayOrder = maxOrder + 1
            };
            _advertisements.Insert(ad);
            _logger.LogInformation("Added new advertisement with ID {AdId}", ad.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding advertisement");
        }
    }

    public List<Advertisement> GetAllAdvertisements()
    {
        return _advertisements.Query().OrderBy(a => a.DisplayOrder).ToList();
    }

    public Advertisement? GetAdvertisement(int id)
    {
        return _advertisements.FindById(id);
    }

    public void DeleteAdvertisement(int id)
    {
        try
        {
            _advertisements.Delete(id);
            _logger.LogInformation("Deleted advertisement {AdId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting advertisement {AdId}", id);
        }
    }

    public void ToggleAdvertisement(int id)
    {
        try
        {
            var ad = _advertisements.FindById(id);
            if (ad != null)
            {
                ad.IsActive = !ad.IsActive;
                _advertisements.Update(ad);
                _logger.LogInformation("Toggled advertisement {AdId} to {Status}", id, ad.IsActive);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling advertisement {AdId}", id);
        }
    }

    // Group Ad Settings
    public GroupAdSettings GetGroupAdSettings(long chatId)
    {
        var settings = _groupAdSettings.FindOne(s => s.ChatId == chatId);
        if (settings == null)
        {
            settings = new GroupAdSettings
            {
                ChatId = chatId,
                AdsEnabled = true,
                LastAdIndex = -1
            };
            _groupAdSettings.Insert(settings);
        }
        return settings;
    }

    public void UpdateGroupAdSettings(GroupAdSettings settings)
    {
        try
        {
            _groupAdSettings.Update(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating group ad settings for {ChatId}", settings.ChatId);
        }
    }

    public void SetGroupAdInterval(long chatId, int? intervalMinutes)
    {
        try
        {
            var settings = GetGroupAdSettings(chatId);
            settings.CustomIntervalMinutes = intervalMinutes;
            _groupAdSettings.Update(settings);
            _logger.LogInformation("Set ad interval for chat {ChatId} to {Interval} minutes",
                chatId, intervalMinutes?.ToString() ?? "default");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting ad interval for {ChatId}", chatId);
        }
    }

    public void UpdateLastAdSent(long chatId, int adIndex)
    {
        try
        {
            var settings = GetGroupAdSettings(chatId);
            settings.LastAdSentAt = DateTime.UtcNow;
            settings.LastAdIndex = adIndex;
            _groupAdSettings.Update(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating last ad sent for {ChatId}", chatId);
        }
    }

    // Ad Statistics
    public void IncrementAdSent(long chatId, int adId)
    {
        try
        {
            var stat = _adStatistics.FindOne(s => s.ChatId == chatId && s.AdId == adId);
            if (stat == null)
            {
                stat = new AdStatistics
                {
                    ChatId = chatId,
                    AdId = adId,
                    TotalSent = 1,
                    LastSentAt = DateTime.UtcNow
                };
                _adStatistics.Insert(stat);
            }
            else
            {
                stat.TotalSent++;
                stat.LastSentAt = DateTime.UtcNow;
                _adStatistics.Update(stat);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing ad statistics for {ChatId}", chatId);
        }
    }

    public List<AdStatistics> GetAdStatistics()
    {
        return _adStatistics.FindAll().ToList();
    }

    public List<AdStatistics> GetAdStatisticsForChat(long chatId)
    {
        return _adStatistics.Query().Where(s => s.ChatId == chatId).ToList();
    }

    public void Dispose()
    {
        _db?.Dispose();
    }
}
