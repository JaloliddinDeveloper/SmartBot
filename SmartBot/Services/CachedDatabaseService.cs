using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SmartBot.Common;
using SmartBot.Models;

namespace SmartBot.Services;

/// <summary>
/// Cached database service wrapper for improved performance
/// Implements decorator pattern over IDatabaseService
/// </summary>
public class CachedDatabaseService : IDatabaseService
{
    private readonly IDatabaseService _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedDatabaseService> _logger;

    public CachedDatabaseService(
        IDatabaseService inner,
        IMemoryCache cache,
        ILogger<CachedDatabaseService> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    // ===== CACHED OPERATIONS =====

    public List<Advertisement> GetAllAdvertisements()
    {
        return _cache.GetOrCreate(Constants.Cache.AllAdvertisements, entry =>
        {
            entry.SetSlidingExpiration(Constants.Cache.DefaultExpiration);
            _logger.LogDebug("Cache MISS: AllAdvertisements");
            return _inner.GetAllAdvertisements();
        }) ?? new List<Advertisement>();
    }

    public List<GroupInfo> GetAllGroups()
    {
        return _cache.GetOrCreate(Constants.Cache.AllGroups, entry =>
        {
            entry.SetSlidingExpiration(Constants.Cache.LongExpiration);
            _logger.LogDebug("Cache MISS: AllGroups");
            return _inner.GetAllGroups();
        }) ?? new List<GroupInfo>();
    }

    public List<GroupInfo> GetAllGroupsIncludingInactive()
    {
        return _cache.GetOrCreate(Constants.Cache.AllGroups + "_all", entry =>
        {
            entry.SetSlidingExpiration(Constants.Cache.LongExpiration);
            _logger.LogDebug("Cache MISS: AllGroupsIncludingInactive");
            return _inner.GetAllGroupsIncludingInactive();
        }) ?? new List<GroupInfo>();
    }

    public GroupInfo? GetGroup(long chatId)
    {
        var cacheKey = $"{Constants.Cache.GroupSettingsPrefix}{chatId}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SetSlidingExpiration(Constants.Cache.LongExpiration);
            _logger.LogDebug("Cache MISS: Group {ChatId}", chatId);
            return _inner.GetGroup(chatId);
        });
    }

    public GroupAdSettings GetGroupAdSettings(long chatId)
    {
        var cacheKey = $"{Constants.Cache.GroupSettingsPrefix}ad_{chatId}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SetSlidingExpiration(Constants.Cache.ShortExpiration);
            _logger.LogDebug("Cache MISS: GroupAdSettings {ChatId}", chatId);
            return _inner.GetGroupAdSettings(chatId);
        }) ?? new GroupAdSettings { ChatId = chatId };
    }

    public Advertisement? GetAdvertisement(int id)
    {
        var cacheKey = $"ad_{id}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SetSlidingExpiration(Constants.Cache.DefaultExpiration);
            return _inner.GetAdvertisement(id);
        });
    }

    public List<AdStatistics> GetAdStatistics()
    {
        return _cache.GetOrCreate("ad_stats_all", entry =>
        {
            entry.SetSlidingExpiration(Constants.Cache.ShortExpiration);
            return _inner.GetAdStatistics();
        }) ?? new List<AdStatistics>();
    }

    public List<AdStatistics> GetAdStatisticsForChat(long chatId)
    {
        var cacheKey = $"ad_stats_{chatId}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SetSlidingExpiration(Constants.Cache.ShortExpiration);
            return _inner.GetAdStatisticsForChat(chatId);
        }) ?? new List<AdStatistics>();
    }

    public BotStatistics GetStatistics(long chatId)
    {
        var cacheKey = $"{Constants.Cache.StatisticsPrefix}{chatId}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SetSlidingExpiration(Constants.Cache.ShortExpiration);
            return _inner.GetStatistics(chatId);
        }) ?? new BotStatistics { ChatId = chatId };
    }

    public List<BotStatistics> GetAllStatistics()
    {
        return _cache.GetOrCreate("stats_all", entry =>
        {
            entry.SetSlidingExpiration(Constants.Cache.ShortExpiration);
            return _inner.GetAllStatistics();
        }) ?? new List<BotStatistics>();
    }

    // ===== CACHE INVALIDATING OPERATIONS =====

    public void AddOrUpdateGroup(long chatId, string title)
    {
        _inner.AddOrUpdateGroup(chatId, title);
        InvalidateGroupCache(chatId);
    }

    public void RemoveGroup(long chatId)
    {
        _inner.RemoveGroup(chatId);
        InvalidateGroupCache(chatId);
    }

    public void AddAdvertisement(string text)
    {
        _inner.AddAdvertisement(text);
        InvalidateAdvertisementCache();
    }

    public void AddAdvertisementWithMedia(string text, string mediaType, string mediaFileId)
    {
        _inner.AddAdvertisementWithMedia(text, mediaType, mediaFileId);
        InvalidateAdvertisementCache();
    }

    public void DeleteAdvertisement(int id)
    {
        _inner.DeleteAdvertisement(id);
        InvalidateAdvertisementCache();
        _cache.Remove($"ad_{id}");
    }

    public void ToggleAdvertisement(int id)
    {
        _inner.ToggleAdvertisement(id);
        InvalidateAdvertisementCache();
        _cache.Remove($"ad_{id}");
    }

    public void UpdateGroupAdSettings(GroupAdSettings settings)
    {
        _inner.UpdateGroupAdSettings(settings);
        InvalidateGroupAdSettingsCache(settings.ChatId);
    }

    public void SetGroupAdInterval(long chatId, int? intervalMinutes)
    {
        _inner.SetGroupAdInterval(chatId, intervalMinutes);
        InvalidateGroupAdSettingsCache(chatId);
    }

    public void UpdateLastAdSent(long chatId, int adIndex)
    {
        _inner.UpdateLastAdSent(chatId, adIndex);
        InvalidateGroupAdSettingsCache(chatId);
    }

    public void IncrementDeletedJoinMessages(long chatId)
    {
        _inner.IncrementDeletedJoinMessages(chatId);
        InvalidateStatisticsCache(chatId);
    }

    public void IncrementDeletedLeaveMessages(long chatId)
    {
        _inner.IncrementDeletedLeaveMessages(chatId);
        InvalidateStatisticsCache(chatId);
    }

    public void IncrementDeletedSpamMessages(long chatId)
    {
        _inner.IncrementDeletedSpamMessages(chatId);
        InvalidateStatisticsCache(chatId);
    }

    public void IncrementAdSent(long chatId, int adId)
    {
        _inner.IncrementAdSent(chatId, adId);
        _cache.Remove("ad_stats_all");
        _cache.Remove($"ad_stats_{chatId}");
    }

    // ===== PASS-THROUGH OPERATIONS (NO CACHING) =====

    public void TrackUserJoin(long userId, long chatId)
    {
        _inner.TrackUserJoin(userId, chatId);
    }

    public bool IsNewUser(long userId, long chatId, int timeWindowMinutes)
    {
        return _inner.IsNewUser(userId, chatId, timeWindowMinutes);
    }

    // ===== CACHE INVALIDATION HELPERS =====

    private void InvalidateGroupCache(long chatId)
    {
        _cache.Remove(Constants.Cache.AllGroups);
        _cache.Remove(Constants.Cache.AllGroups + "_all");
        _cache.Remove($"{Constants.Cache.GroupSettingsPrefix}{chatId}");
        _logger.LogDebug("Invalidated group cache for {ChatId}", chatId);
    }

    private void InvalidateAdvertisementCache()
    {
        _cache.Remove(Constants.Cache.AllAdvertisements);
        _logger.LogDebug("Invalidated advertisement cache");
    }

    private void InvalidateGroupAdSettingsCache(long chatId)
    {
        _cache.Remove($"{Constants.Cache.GroupSettingsPrefix}ad_{chatId}");
        _logger.LogDebug("Invalidated group ad settings cache for {ChatId}", chatId);
    }

    private void InvalidateStatisticsCache(long chatId)
    {
        _cache.Remove($"{Constants.Cache.StatisticsPrefix}{chatId}");
        _cache.Remove("stats_all");
        _logger.LogDebug("Invalidated statistics cache for {ChatId}", chatId);
    }
}
