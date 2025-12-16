using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SmartBot.Services;

/// <summary>
/// Metrics collection service for business and performance monitoring
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Records a message processed
    /// </summary>
    void RecordMessageProcessed(string messageType);

    /// <summary>
    /// Records a spam message detected
    /// </summary>
    void RecordSpamDetected();

    /// <summary>
    /// Records an advertisement sent
    /// </summary>
    void RecordAdSent();

    /// <summary>
    /// Records an error
    /// </summary>
    void RecordError(string errorType);

    /// <summary>
    /// Records operation duration
    /// </summary>
    void RecordDuration(string operationName, TimeSpan duration);

    /// <summary>
    /// Gets current metrics
    /// </summary>
    MetricsSnapshot GetSnapshot();
}

public record MetricsSnapshot(
    long TotalMessagesProcessed,
    long SpamMessagesDetected,
    long AdvertisementsSent,
    long TotalErrors,
    Dictionary<string, long> MessageTypeCounts,
    Dictionary<string, long> ErrorTypeCounts,
    Dictionary<string, TimeSpan> AverageDurations,
    DateTime SnapshotTime,
    TimeSpan Uptime
);

public class MetricsService : IMetricsService
{
    private readonly ILogger<MetricsService> _logger;
    private readonly Stopwatch _uptime;

    // Counters
    private long _totalMessagesProcessed = 0;
    private long _spamMessagesDetected = 0;
    private long _advertisementsSent = 0;
    private long _totalErrors = 0;

    // Detailed counters
    private readonly Dictionary<string, long> _messageTypeCounts = new();
    private readonly Dictionary<string, long> _errorTypeCounts = new();
    private readonly Dictionary<string, (long count, TimeSpan total)> _durations = new();
    private readonly object _lock = new();

    public MetricsService(ILogger<MetricsService> logger)
    {
        _logger = logger;
        _uptime = Stopwatch.StartNew();

        // Start periodic logging
        _ = StartPeriodicLoggingAsync();
    }

    public void RecordMessageProcessed(string messageType)
    {
        Interlocked.Increment(ref _totalMessagesProcessed);

        lock (_lock)
        {
            if (!_messageTypeCounts.ContainsKey(messageType))
            {
                _messageTypeCounts[messageType] = 0;
            }
            _messageTypeCounts[messageType]++;
        }
    }

    public void RecordSpamDetected()
    {
        Interlocked.Increment(ref _spamMessagesDetected);
    }

    public void RecordAdSent()
    {
        Interlocked.Increment(ref _advertisementsSent);
    }

    public void RecordError(string errorType)
    {
        Interlocked.Increment(ref _totalErrors);

        lock (_lock)
        {
            if (!_errorTypeCounts.ContainsKey(errorType))
            {
                _errorTypeCounts[errorType] = 0;
            }
            _errorTypeCounts[errorType]++;
        }
    }

    public void RecordDuration(string operationName, TimeSpan duration)
    {
        lock (_lock)
        {
            if (!_durations.ContainsKey(operationName))
            {
                _durations[operationName] = (0, TimeSpan.Zero);
            }

            var (count, total) = _durations[operationName];
            _durations[operationName] = (count + 1, total + duration);
        }
    }

    public MetricsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var averages = _durations.ToDictionary(
                kvp => kvp.Key,
                kvp => TimeSpan.FromMilliseconds(
                    kvp.Value.total.TotalMilliseconds / Math.Max(1, kvp.Value.count))
            );

            return new MetricsSnapshot(
                TotalMessagesProcessed: _totalMessagesProcessed,
                SpamMessagesDetected: _spamMessagesDetected,
                AdvertisementsSent: _advertisementsSent,
                TotalErrors: _totalErrors,
                MessageTypeCounts: new Dictionary<string, long>(_messageTypeCounts),
                ErrorTypeCounts: new Dictionary<string, long>(_errorTypeCounts),
                AverageDurations: averages,
                SnapshotTime: DateTime.UtcNow,
                Uptime: _uptime.Elapsed
            );
        }
    }

    private async Task StartPeriodicLoggingAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(15));

                var snapshot = GetSnapshot();

                _logger.LogInformation(
                    "📊 METRICS: Messages: {Messages}, Spam: {Spam}, Ads: {Ads}, Errors: {Errors}, Uptime: {Uptime:hh\\:mm\\:ss}",
                    snapshot.TotalMessagesProcessed,
                    snapshot.SpamMessagesDetected,
                    snapshot.AdvertisementsSent,
                    snapshot.TotalErrors,
                    snapshot.Uptime
                );

                if (snapshot.ErrorTypeCounts.Any())
                {
                    _logger.LogWarning(
                        "Error breakdown: {Errors}",
                        string.Join(", ", snapshot.ErrorTypeCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}"))
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metrics periodic logging");
            }
        }
    }
}
