using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartBot.Models;
using Telegram.Bot;

namespace SmartBot.Services;

public class AdBackgroundService : BackgroundService
{
    private readonly ILogger<AdBackgroundService> _logger;
    private readonly IAdvertisingService _advertisingService;
    private readonly ITelegramBotClient _botClient;
    private readonly AdvertisingConfig _config;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Check every minute

    public AdBackgroundService(
        ILogger<AdBackgroundService> logger,
        IAdvertisingService advertisingService,
        ITelegramBotClient botClient,
        AdvertisingConfig config)
    {
        _logger = logger;
        _advertisingService = advertisingService;
        _botClient = botClient;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ad Background Service started");

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_config.Enabled)
                {
                    _logger.LogDebug("Checking groups for ad delivery...");
                    await _advertisingService.SendAdsToGroupsAsync(_botClient, stoppingToken);
                }
                else
                {
                    _logger.LogDebug("Advertising is disabled");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ad background service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Ad Background Service stopped");
    }
}
