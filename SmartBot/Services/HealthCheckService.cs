using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace SmartBot.Services;

/// <summary>
/// Health check service for monitoring bot status
/// </summary>
public class BotHealthCheck : IHealthCheck
{
    private readonly ITelegramBotClient _botClient;
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<BotHealthCheck> _logger;

    public BotHealthCheck(
        ITelegramBotClient botClient,
        IDatabaseService databaseService,
        ILogger<BotHealthCheck> logger)
    {
        _botClient = botClient;
        _databaseService = databaseService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new Dictionary<string, object>();

            // 1. Check Telegram Bot API connection
            var botInfo = await _botClient.GetMeAsync(cancellationToken);
            data.Add("bot_username", botInfo.Username ?? "unknown");
            data.Add("bot_id", botInfo.Id);

            // 2. Check database connectivity
            var groupCount = _databaseService.GetAllGroups().Count;
            data.Add("active_groups", groupCount);

            var totalStats = _databaseService.GetAllStatistics().Count;
            data.Add("total_statistics", totalStats);

            // 3. Check advertisements
            var adCount = _databaseService.GetAllAdvertisements().Count;
            var activeAdCount = _databaseService.GetAllAdvertisements().Count(a => a.IsActive);
            data.Add("total_ads", adCount);
            data.Add("active_ads", activeAdCount);

            // 4. Memory check
            var memoryUsed = GC.GetTotalMemory(false) / 1024 / 1024; // MB
            data.Add("memory_mb", memoryUsed);

            if (memoryUsed > 500) // Warning if >500MB
            {
                return HealthCheckResult.Degraded(
                    "High memory usage",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                "Bot is healthy",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return HealthCheckResult.Unhealthy(
                "Health check failed",
                exception: ex);
        }
    }
}

/// <summary>
/// Database health check
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(
        IDatabaseService databaseService,
        ILogger<DatabaseHealthCheck> logger)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try a simple database operation
            _ = _databaseService.GetAllGroups();

            return Task.FromResult(HealthCheckResult.Healthy("Database is accessible"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Database is not accessible",
                exception: ex));
        }
    }
}
