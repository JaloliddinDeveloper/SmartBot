using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SmartBot.Common;

namespace SmartBot.Services;

/// <summary>
/// Rate limiting service to prevent API abuse and Telegram ban
/// </summary>
public interface IRateLimitingService
{
    /// <summary>
    /// Checks if user is allowed to make a request
    /// </summary>
    bool IsUserAllowed(long userId);

    /// <summary>
    /// Checks if chat is allowed to receive a message
    /// </summary>
    bool IsChatAllowed(long chatId);

    /// <summary>
    /// Acquires a slot for sending message to Telegram API
    /// </summary>
    Task<bool> AcquireTelegramApiSlotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a Telegram API slot
    /// </summary>
    void ReleaseTelegramApiSlot();

    /// <summary>
    /// Gets current statistics
    /// </summary>
    RateLimitStats GetStats();
}

public record RateLimitStats(
    int ActiveUsers,
    int ActiveChats,
    int TelegramApiSlots,
    int TotalBlockedRequests
);

public class RateLimitingService : IRateLimitingService
{
    private readonly ILogger<RateLimitingService> _logger;

    // User rate limiting: (UserId, (RequestCount, ResetTime))
    private readonly ConcurrentDictionary<long, (int count, DateTime reset)> _userLimits = new();

    // Chat rate limiting: (ChatId, (RequestCount, ResetTime))
    private readonly ConcurrentDictionary<long, (int count, DateTime reset)> _chatLimits = new();

    // Telegram API semaphore (max 30 messages/second)
    private readonly SemaphoreSlim _telegramApiSemaphore;

    // Statistics
    private int _totalBlockedRequests = 0;

    public RateLimitingService(ILogger<RateLimitingService> logger)
    {
        _logger = logger;
        _telegramApiSemaphore = new SemaphoreSlim(
            Constants.RateLimits.TelegramApiMessagesPerSecond,
            Constants.RateLimits.TelegramApiMessagesPerSecond
        );

        // Start cleanup task
        _ = StartCleanupTaskAsync();
    }

    public bool IsUserAllowed(long userId)
    {
        var now = DateTime.UtcNow;
        var limit = Constants.RateLimits.MaxRequestsPerUserPerMinute;

        var (count, reset) = _userLimits.AddOrUpdate(
            userId,
            (1, now.AddMinutes(1)),
            (key, value) =>
            {
                if (now > value.reset)
                {
                    // Reset window
                    return (1, now.AddMinutes(1));
                }

                return value.count < limit
                    ? (value.count + 1, value.reset)
                    : (value.count, value.reset);
            });

        if (count > limit)
        {
            Interlocked.Increment(ref _totalBlockedRequests);
            _logger.LogWarning(
                "User {UserId} exceeded rate limit ({Count}/{Limit})",
                userId, count, limit);
            return false;
        }

        return true;
    }

    public bool IsChatAllowed(long chatId)
    {
        var now = DateTime.UtcNow;
        var limit = Constants.RateLimits.MaxRequestsPerChatPerMinute;

        var (count, reset) = _chatLimits.AddOrUpdate(
            chatId,
            (1, now.AddMinutes(1)),
            (key, value) =>
            {
                if (now > value.reset)
                {
                    return (1, now.AddMinutes(1));
                }

                return value.count < limit
                    ? (value.count + 1, value.reset)
                    : (value.count, value.reset);
            });

        if (count > limit)
        {
            Interlocked.Increment(ref _totalBlockedRequests);
            _logger.LogWarning(
                "Chat {ChatId} exceeded rate limit ({Count}/{Limit})",
                chatId, count, limit);
            return false;
        }

        return true;
    }

    public async Task<bool> AcquireTelegramApiSlotAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Wait for available slot with timeout
            var acquired = await _telegramApiSemaphore.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);

            if (!acquired)
            {
                _logger.LogWarning("Failed to acquire Telegram API slot (timeout)");
            }

            // Release slot after 1 second (to maintain 30 msg/sec rate)
            if (acquired)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000, CancellationToken.None);
                    _telegramApiSemaphore.Release();
                }, CancellationToken.None);
            }

            return acquired;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public void ReleaseTelegramApiSlot()
    {
        try
        {
            _telegramApiSemaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already at max capacity, ignore
        }
    }

    public RateLimitStats GetStats()
    {
        return new RateLimitStats(
            ActiveUsers: _userLimits.Count,
            ActiveChats: _chatLimits.Count,
            TelegramApiSlots: _telegramApiSemaphore.CurrentCount,
            TotalBlockedRequests: _totalBlockedRequests
        );
    }

    /// <summary>
    /// Periodically cleans up expired entries
    /// </summary>
    private async Task StartCleanupTaskAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5));

                var now = DateTime.UtcNow;
                var expiredUsers = _userLimits
                    .Where(kvp => kvp.Value.reset < now)
                    .Select(kvp => kvp.Key)
                    .ToList();

                var expiredChats = _chatLimits
                    .Where(kvp => kvp.Value.reset < now)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var userId in expiredUsers)
                {
                    _userLimits.TryRemove(userId, out _);
                }

                foreach (var chatId in expiredChats)
                {
                    _chatLimits.TryRemove(chatId, out _);
                }

                _logger.LogDebug(
                    "Cleaned up {UserCount} user limits and {ChatCount} chat limits",
                    expiredUsers.Count, expiredChats.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in rate limit cleanup task");
            }
        }
    }
}
