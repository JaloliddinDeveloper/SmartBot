using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartBot.Models;
using SmartBot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;

namespace SmartBot;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🤖 SmartBot - Professional Telegram Group Moderator");
        Console.WriteLine("═══════════════════════════════════════════════════");

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Get settings
        var settings = serviceProvider.GetRequiredService<AppSettings>();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        // Validate bot token
        if (string.IsNullOrWhiteSpace(settings.BotConfiguration.BotToken) ||
            settings.BotConfiguration.BotToken == "YOUR_BOT_TOKEN_HERE")
        {
            logger.LogError("Bot token not configured! Please update appsettings.json");
            Console.WriteLine("\n❌ Xato: Bot token sozlanmagan!");
            Console.WriteLine("📝 appsettings.json faylida BotToken ni o'zgartiring.");
            Console.WriteLine("\nBot tokenni olish uchun @BotFather ga murojaat qiling:");
            Console.WriteLine("1. Telegramda @BotFather ni oching");
            Console.WriteLine("2. /newbot buyrug'ini yuboring");
            Console.WriteLine("3. Bot nomi va username kiriting");
            Console.WriteLine("4. Token ni nusxalab appsettings.json ga qo'ying");
            return;
        }

        // Validate admin user ID
        if (settings.BotConfiguration.AdminUserId == 0)
        {
            logger.LogWarning("Admin user ID not configured. Admin commands will not work.");
            Console.WriteLine("\n⚠️  Ogohlantirish: Admin user ID sozlanmagan!");
            Console.WriteLine("📝 appsettings.json faylida AdminUserId ni o'zgartiring.");
            Console.WriteLine("\nUser ID ni bilish uchun @userinfobot ga murojaat qiling.");
        }

        try
        {
            // Create bot client
            var botClient = new TelegramBotClient(settings.BotConfiguration.BotToken);

            // Get bot info
            var me = await botClient.GetMeAsync();
            logger.LogInformation("Bot started: @{Username}", me.Username);
            Console.WriteLine($"\n✅ Bot ishga tushdi: @{me.Username}");
            Console.WriteLine($"📊 Bot ID: {me.Id}");
            Console.WriteLine("\n🔧 Yoqilgan funksiyalar:");
            if (settings.Features.AutoDeleteJoinLeaveMessages)
                Console.WriteLine("   ✅ Kirish/Chiqish xabarlarini o'chirish");
            if (settings.Features.SpamDetection)
                Console.WriteLine("   ✅ Spam aniqlash");
            if (settings.Features.EnableStatistics)
                Console.WriteLine("   ✅ Statistika yig'ish");
            if (settings.Advertising.Enabled)
                Console.WriteLine($"   ✅ Reklama tizimi ({settings.Advertising.DefaultIntervalMinutes} daqiqa interval)");

            Console.WriteLine("\n📋 Spam kalit so'zlar:");
            foreach (var keyword in settings.SpamDetection.Keywords.Take(5))
            {
                Console.WriteLine($"   • {keyword}");
            }
            if (settings.SpamDetection.Keywords.Count > 5)
                Console.WriteLine($"   ... va yana {settings.SpamDetection.Keywords.Count - 5} ta");

            Console.WriteLine("\n🚀 Bot ishlayapti. To'xtatish uchun Ctrl+C ni bosing.");
            Console.WriteLine("═══════════════════════════════════════════════════\n");

            // Setup cancellation
            using var cts = new CancellationTokenSource();

            // Start advertising timer in background if enabled
            Task? adTimerTask = null;
            if (settings.Advertising.Enabled && settings.Advertising.AutoStartOnBotStartup)
            {
                var advertisingService = serviceProvider.GetRequiredService<IAdvertisingService>();
                adTimerTask = Task.Run(async () =>
                {
                    logger.LogInformation("Ad timer service started");
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await advertisingService.SendAdsToGroupsAsync(botClient, cts.Token);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error in ad timer service");
                        }
                        await Task.Delay(TimeSpan.FromSeconds(60), cts.Token);
                    }
                    logger.LogInformation("Ad timer service stopped");
                }, cts.Token);
            }

            Console.CancelKeyPress += (_, e) =>
            {
                logger.LogInformation("Shutdown requested");
                Console.WriteLine("\n\n🛑 Bot to'xtatilmoqda...");
                e.Cancel = true;
                cts.Cancel();
            };

            // Get update handler
            var updateHandler = serviceProvider.GetRequiredService<BotUpdateHandler>();

            // Configure receiving options
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[]
                {
                    Telegram.Bot.Types.Enums.UpdateType.Message,
                    Telegram.Bot.Types.Enums.UpdateType.MyChatMember,
                    Telegram.Bot.Types.Enums.UpdateType.ChatMember
                }
            };

            // Start receiving updates
            await botClient.ReceiveAsync(
                updateHandler,
                receiverOptions,
                cts.Token
            );

            Console.WriteLine("\n👋 Bot to'xtatildi. Xayr!");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Fatal error");
            Console.WriteLine($"\n❌ Fatal xato: {ex.Message}");
            Console.WriteLine("Batafsil ma'lumot logda mavjud.");
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddConfiguration(configuration.GetSection("Logging"));
        });

        // Bind configuration
        var settings = new AppSettings();
        configuration.Bind(settings);
        services.AddSingleton(settings);
        services.AddSingleton(settings.BotConfiguration);
        services.AddSingleton(settings.Features);
        services.AddSingleton(settings.SpamDetection);
        services.AddSingleton(settings.Advertising);

        // Register services
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<ISpamDetectionService, SpamDetectionService>();
        services.AddSingleton<IAdvertisingService, AdvertisingService>();
        services.AddSingleton<BotUpdateHandler>();
    }
}
