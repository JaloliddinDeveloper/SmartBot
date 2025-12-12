using Microsoft.Extensions.Logging;
using SmartBot.Models;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SmartBot.Services;

public class BotUpdateHandler : IUpdateHandler
{
    private readonly ILogger<BotUpdateHandler> _logger;
    private readonly IDatabaseService _databaseService;
    private readonly ISpamDetectionService _spamDetectionService;
    private readonly IAdvertisingService _advertisingService;
    private readonly AppSettings _settings;

    public BotUpdateHandler(
        ILogger<BotUpdateHandler> logger,
        IDatabaseService databaseService,
        ISpamDetectionService spamDetectionService,
        IAdvertisingService advertisingService,
        AppSettings settings)
    {
        _logger = logger;
        _databaseService = databaseService;
        _spamDetectionService = spamDetectionService;
        _advertisingService = advertisingService;
        _settings = settings;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            var handler = update.Type switch
            {
                UpdateType.Message => HandleMessageAsync(botClient, update.Message!, cancellationToken),
                UpdateType.MyChatMember => HandleMyChatMemberAsync(botClient, update.MyChatMember!, cancellationToken),
                UpdateType.ChatMember => HandleChatMemberAsync(botClient, update.ChatMember!, cancellationToken),
                _ => Task.CompletedTask
            };

            await handler;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update");
        }
    }

    private async Task HandleMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.Chat.Type == ChatType.Private)
        {
            await HandlePrivateMessageAsync(botClient, message, cancellationToken);
            return;
        }

        // Handle group messages
        var chatId = message.Chat.Id;

        // Delete join/leave messages
        if (_settings.Features.AutoDeleteJoinLeaveMessages)
        {
            if (message.NewChatMembers != null && message.NewChatMembers.Length > 0)
            {
                await DeleteMessageSafely(botClient, chatId, message.MessageId, cancellationToken);
                _databaseService.IncrementDeletedJoinMessages(chatId);

                // Track new users
                foreach (var user in message.NewChatMembers)
                {
                    _databaseService.TrackUserJoin(user.Id, chatId);
                    _logger.LogInformation("User {UserId} joined chat {ChatId}", user.Id, chatId);
                }
                return;
            }

            if (message.LeftChatMember != null)
            {
                await DeleteMessageSafely(botClient, chatId, message.MessageId, cancellationToken);
                _databaseService.IncrementDeletedLeaveMessages(chatId);
                _logger.LogInformation("User {UserId} left chat {ChatId}", message.LeftChatMember.Id, chatId);
                return;
            }
        }

        // Check for spam in both text and caption
        if (_settings.Features.SpamDetection)
        {
            // Check message text or caption (for photos, videos, documents)
            var contentToCheck = message.Text ?? message.Caption;

            if (!string.IsNullOrWhiteSpace(contentToCheck))
            {
                if (_spamDetectionService.IsSpam(contentToCheck, message.From!.Id, chatId))
                {
                    await DeleteMessageSafely(botClient, chatId, message.MessageId, cancellationToken);
                    _databaseService.IncrementDeletedSpamMessages(chatId);

                    // Optionally warn the user (commented out to avoid spam)
                    // await botClient.SendTextMessageAsync(
                    //     chatId,
                    //     $"⚠️ Spam habar o'chirildi!",
                    //     cancellationToken: cancellationToken
                    // );
                    return;
                }
            }
        }

        // Handle admin commands
        if (message.From?.Id == _settings.BotConfiguration.AdminUserId && message.Text != null)
        {
            await HandleAdminCommandAsync(botClient, message, cancellationToken);
        }
    }

    private async Task HandlePrivateMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.Text == null)
            return;

        if (message.From?.Id != _settings.BotConfiguration.AdminUserId)
        {
            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                "Salom! Men guruhlar uchun moderator botman. Meni guruhga qo'shing va admin qiling!",
                cancellationToken: cancellationToken
            );
            return;
        }

        // Admin commands
        await HandleAdminCommandAsync(botClient, message, cancellationToken);
    }

    private async Task HandleAdminCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var command = message.Text?.ToLower().Trim();

        switch (command)
        {
            case "/start":
            case "/help":
                await SendHelpMessageAsync(botClient, message.Chat.Id, cancellationToken);
                break;

            case "/stats":
            case "/statistics":
                await SendStatisticsAsync(botClient, message.Chat.Id, cancellationToken);
                break;

            case "/groups":
                await SendGroupsListAsync(botClient, message.Chat.Id, cancellationToken);
                break;

            case var cmd when cmd?.StartsWith("/addad ") == true:
                await HandleAddAdAsync(botClient, message, cancellationToken);
                break;

            case "/listads":
                await HandleListAdsAsync(botClient, message.Chat.Id, cancellationToken);
                break;

            case var cmd when cmd?.StartsWith("/deletead ") == true:
                await HandleDeleteAdAsync(botClient, message, cancellationToken);
                break;

            case var cmd when cmd?.StartsWith("/togglead ") == true:
                await HandleToggleAdAsync(botClient, message, cancellationToken);
                break;

            case "/adstats":
                await HandleAdStatsAsync(botClient, message.Chat.Id, cancellationToken);
                break;

            case var cmd when cmd?.StartsWith("/setadinterval ") == true:
                await HandleSetAdIntervalAsync(botClient, message, cancellationToken);
                break;

            case "/togglegroupads":
                await HandleToggleGroupAdsAsync(botClient, message, cancellationToken);
                break;
        }
    }

    private async Task SendHelpMessageAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var helpText = @"🤖 SmartBot - Professional Telegram Group Moderator

📋 Funksiyalar:
✅ Kirish/Chiqish xabarlarini avtomatik o'chirish
✅ Spam va reklamalarni aniqlash va o'chirish
✅ Yangi userlarning habarlarini nazorat qilish
✅ Har bir guruh uchun statistika
✅ Avtomatik reklama tizimi

👮 Admin Buyruqlari:
/help - Yordam
/stats - Barcha guruhlar statistikasi
/groups - Bot qo'shilgan guruhlar ro'yxati

📢 Reklama Buyruqlari:
/addad <matn> - Yangi reklama qo'shish
/listads - Barcha reklamalar ro'yxati
/deletead <id> - Reklamani o'chirish
/togglead <id> - Reklamani yoqish/o'chirish
/adstats - Reklama statistikasi
/setadinterval <daqiqa> - Guruh uchun interval o'rnatish
/togglegroupads - Guruhda reklamalarni yoqish/o'chirish

🔧 Sozlashlar:
- Bot guruhda admin bo'lishi kerak
- 'Delete messages' huquqi bo'lishi kerak";

        await botClient.SendTextMessageAsync(
            chatId,
            helpText,
            cancellationToken: cancellationToken
        );
    }

    private async Task SendStatisticsAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var allStats = _databaseService.GetAllStatistics();
        var groups = _databaseService.GetAllGroups();

        if (!allStats.Any())
        {
            await botClient.SendTextMessageAsync(
                chatId,
                "Hozircha statistika yo'q.",
                cancellationToken: cancellationToken
            );
            return;
        }

        var statsText = "📊 Bot Statistikasi:\n\n";

        foreach (var stat in allStats)
        {
            var group = groups.FirstOrDefault(g => g.ChatId == stat.ChatId);
            var groupTitle = group?.Title ?? stat.ChatTitle ?? $"Chat {stat.ChatId}";

            statsText += $"📍 {groupTitle}\n";
            statsText += $"   👋 Kirish xabarlari: {stat.DeletedJoinMessages}\n";
            statsText += $"   👋 Chiqish xabarlari: {stat.DeletedLeaveMessages}\n";
            statsText += $"   🚫 Spam xabarlari: {stat.DeletedSpamMessages}\n";
            statsText += $"   ✅ Jami: {stat.DeletedJoinMessages + stat.DeletedLeaveMessages + stat.DeletedSpamMessages}\n\n";
        }

        var totalDeleted = allStats.Sum(s => s.DeletedJoinMessages + s.DeletedLeaveMessages + s.DeletedSpamMessages);
        statsText += $"🎯 Umumiy o'chirilgan xabarlar: {totalDeleted}";

        await botClient.SendTextMessageAsync(
            chatId,
            statsText,
            cancellationToken: cancellationToken
        );
    }

    private async Task SendGroupsListAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var groups = _databaseService.GetAllGroups();

        if (!groups.Any())
        {
            await botClient.SendTextMessageAsync(
                chatId,
                "Bot hozircha hech qanday guruhda emas.",
                cancellationToken: cancellationToken
            );
            return;
        }

        var groupsText = $"📁 Bot {groups.Count} ta guruhda:\n\n";

        foreach (var group in groups.OrderByDescending(g => g.JoinedAt))
        {
            groupsText += $"• {group.Title ?? "Noma'lum"}\n";
            groupsText += $"  ID: {group.ChatId}\n";
            groupsText += $"  Qo'shildi: {group.JoinedAt:dd.MM.yyyy HH:mm}\n\n";
        }

        await botClient.SendTextMessageAsync(
            chatId,
            groupsText,
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleAddAdAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var text = message.Text?.Substring(7).Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                "❌ Reklama matni bo'sh bo'lishi mumkin emas!\n\nMisol: /addad Bizning yangi mahsulotimiz!",
                cancellationToken: cancellationToken
            );
            return;
        }

        _databaseService.AddAdvertisement(text);

        await botClient.SendTextMessageAsync(
            message.Chat.Id,
            $"✅ Reklama qo'shildi!\n\n📝 Matn: {text}",
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleListAdsAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var ads = _databaseService.GetAllAdvertisements();

        if (!ads.Any())
        {
            await botClient.SendTextMessageAsync(
                chatId,
                "📭 Hozircha reklamalar yo'q.\n\nYangi reklama qo'shish: /addad <matn>",
                cancellationToken: cancellationToken
            );
            return;
        }

        var adsText = "📢 Reklamalar ro'yxati:\n\n";

        foreach (var ad in ads)
        {
            var status = ad.IsActive ? "✅ Aktiv" : "❌ O'chirilgan";
            adsText += $"ID: {ad.Id} - {status}\n";
            adsText += $"📝 {ad.Text}\n";
            adsText += $"📅 Yaratildi: {ad.CreatedAt:dd.MM.yyyy HH:mm}\n\n";
        }

        await botClient.SendTextMessageAsync(
            chatId,
            adsText,
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleDeleteAdAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var idStr = message.Text?.Substring(10).Trim();

        if (!int.TryParse(idStr, out var adId))
        {
            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                "❌ Noto'g'ri ID format!\n\nMisol: /deletead 1",
                cancellationToken: cancellationToken
            );
            return;
        }

        var ad = _databaseService.GetAdvertisement(adId);
        if (ad == null)
        {
            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                $"❌ ID {adId} bilan reklama topilmadi!",
                cancellationToken: cancellationToken
            );
            return;
        }

        _databaseService.DeleteAdvertisement(adId);

        await botClient.SendTextMessageAsync(
            message.Chat.Id,
            $"✅ Reklama o'chirildi!\n\n📝 Matn: {ad.Text}",
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleToggleAdAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var idStr = message.Text?.Substring(10).Trim();

        if (!int.TryParse(idStr, out var adId))
        {
            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                "❌ Noto'g'ri ID format!\n\nMisol: /togglead 1",
                cancellationToken: cancellationToken
            );
            return;
        }

        var ad = _databaseService.GetAdvertisement(adId);
        if (ad == null)
        {
            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                $"❌ ID {adId} bilan reklama topilmadi!",
                cancellationToken: cancellationToken
            );
            return;
        }

        _databaseService.ToggleAdvertisement(adId);
        var newStatus = !ad.IsActive;

        await botClient.SendTextMessageAsync(
            message.Chat.Id,
            $"✅ Reklama {(newStatus ? "yoqildi" : "o'chirildi")}!\n\n📝 Matn: {ad.Text}",
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleAdStatsAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var stats = _databaseService.GetAdStatistics();
        var ads = _databaseService.GetAllAdvertisements();

        if (!stats.Any())
        {
            await botClient.SendTextMessageAsync(
                chatId,
                "📭 Hozircha reklama statistikasi yo'q.",
                cancellationToken: cancellationToken
            );
            return;
        }

        var statsText = "📊 Reklama Statistikasi:\n\n";

        var groupedByAd = stats.GroupBy(s => s.AdId);

        foreach (var adGroup in groupedByAd)
        {
            var ad = ads.FirstOrDefault(a => a.Id == adGroup.Key);
            var adText = ad?.Text ?? "O'chirilgan reklama";
            var totalSent = adGroup.Sum(s => s.TotalSent);

            statsText += $"📢 Reklama #{adGroup.Key}\n";
            statsText += $"📝 {(adText.Length > 50 ? adText.Substring(0, 50) + "..." : adText)}\n";
            statsText += $"📨 Jami yuborildi: {totalSent}\n";
            statsText += $"🏘️ Guruhlar: {adGroup.Count()}\n\n";
        }

        var totalAllAds = stats.Sum(s => s.TotalSent);
        statsText += $"🎯 Umumiy yuborilgan: {totalAllAds}";

        await botClient.SendTextMessageAsync(
            chatId,
            statsText,
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleSetAdIntervalAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.Chat.Type == ChatType.Private)
        {
            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                "❌ Bu buyruq faqat guruhlarda ishlaydi!",
                cancellationToken: cancellationToken
            );
            return;
        }

        var minutesStr = message.Text?.Substring(15).Trim();

        if (!int.TryParse(minutesStr, out var minutes) || minutes < 1)
        {
            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                "❌ Noto'g'ri format! Musbat son kiriting.\n\nMisol: /setadinterval 30",
                cancellationToken: cancellationToken
            );
            return;
        }

        _databaseService.SetGroupAdInterval(message.Chat.Id, minutes);

        await botClient.SendTextMessageAsync(
            message.Chat.Id,
            $"✅ Reklama intervali {minutes} daqiqaga o'rnatildi!",
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleToggleGroupAdsAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.Chat.Type == ChatType.Private)
        {
            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                "❌ Bu buyruq faqat guruhlarda ishlaydi!",
                cancellationToken: cancellationToken
            );
            return;
        }

        var settings = _databaseService.GetGroupAdSettings(message.Chat.Id);
        settings.AdsEnabled = !settings.AdsEnabled;
        _databaseService.UpdateGroupAdSettings(settings);

        await botClient.SendTextMessageAsync(
            message.Chat.Id,
            $"✅ Guruhda reklamalar {(settings.AdsEnabled ? "yoqildi" : "o'chirildi")}!",
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleMyChatMemberAsync(ITelegramBotClient botClient, ChatMemberUpdated myChatMember, CancellationToken cancellationToken)
    {
        var chatId = myChatMember.Chat.Id;
        var chatTitle = myChatMember.Chat.Title ?? "Unknown";

        if (myChatMember.NewChatMember.Status == ChatMemberStatus.Member ||
            myChatMember.NewChatMember.Status == ChatMemberStatus.Administrator)
        {
            // Bot was added to the group
            _databaseService.AddOrUpdateGroup(chatId, chatTitle);
            _logger.LogInformation("Bot added to group: {Title} ({ChatId})", chatTitle, chatId);

            try
            {
                var welcomeMessage = @"👋 Salom! Men SmartBot - professional guruh moderatori!

🎯 MENING IMKONIYATLARIM:

✅ Avtomatik Tozalash
• Kirish/Chiqish xabarlarini darhol o'chiraman
• Guruhingizni toza va tartibli saqlaydi
• Yangi a'zolar haqida statistika yig'aman

🛡️ Spam Himoyasi
• Spam va keraksiz reklamalarni aniqlaydi
• Shubhali havolalarni bloklaydi
• Yangi userlarning spam yuborishini oldini oladi
• 7+ spam kalit so'zlarini aniqlaydi

📊 Statistika
• O'chirilgan xabarlar hisobi
• Har bir guruh uchun alohida statistika
• Admin uchun batafsil hisobotlar

🔧 SOZLASH:
1. Meni admin qiling
2. 'Delete messages' huquqini bering
3. Tayyor! Endi men avtomatik ishlayman

💡 Administrator buyruqlari:
• /help - Barcha buyruqlar ro'yxati
• /stats - Guruh statistikasi
• /groups - Bot qo'shilgan guruhlar

Guruhingizni toza va xavfsiz saqlashda yordam beraman! 🚀";

                await botClient.SendTextMessageAsync(
                    chatId,
                    welcomeMessage,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send welcome message to group {ChatId}", chatId);
            }
        }
        else if (myChatMember.NewChatMember.Status == ChatMemberStatus.Left ||
                 myChatMember.NewChatMember.Status == ChatMemberStatus.Kicked)
        {
            // Bot was removed from the group
            _databaseService.RemoveGroup(chatId);
            _logger.LogInformation("Bot removed from group: {Title} ({ChatId})", chatTitle, chatId);
        }
    }

    private async Task HandleChatMemberAsync(ITelegramBotClient botClient, ChatMemberUpdated chatMember, CancellationToken cancellationToken)
    {
        // Track when users join via this event too
        if (chatMember.NewChatMember.Status == ChatMemberStatus.Member ||
            chatMember.NewChatMember.Status == ChatMemberStatus.Restricted)
        {
            _databaseService.TrackUserJoin(chatMember.NewChatMember.User.Id, chatMember.Chat.Id);
        }
    }

    private async Task DeleteMessageSafely(ITelegramBotClient botClient, long chatId, int messageId, CancellationToken cancellationToken)
    {
        try
        {
            await botClient.DeleteMessageAsync(chatId, messageId, cancellationToken);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("message to delete not found"))
        {
            _logger.LogWarning("Message {MessageId} in chat {ChatId} was already deleted", messageId, chatId);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("not enough rights"))
        {
            _logger.LogWarning("Bot doesn't have rights to delete messages in chat {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message {MessageId} in chat {ChatId}", messageId, chatId);
        }
    }

    public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogError(exception, "Polling error: {ErrorMessage}", errorMessage);
        return Task.CompletedTask;
    }
}
