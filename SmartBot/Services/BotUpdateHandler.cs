using LiteDB;
using Microsoft.Extensions.Logging;
using SmartBot.Models;
using System;
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
        var chatTitle = message.Chat.Title ?? "Unknown Group";

        // Ensure the group is tracked in database (in case MyChatMember event was missed)
        _databaseService.AddOrUpdateGroup(chatId, chatTitle);

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

        // Handle commands in groups
        if (message.Text != null)
        {
            // Check if user is bot owner (full admin)
            if (message.From?.Id == _settings.BotConfiguration.AdminUserId)
            {
                await HandleAdminCommandAsync(botClient, message, cancellationToken);
            }
            // Check if user is group admin
            else if (await IsUserGroupAdmin(botClient, chatId, message.From!.Id, cancellationToken))
            {
                await HandleGroupAdminCommandAsync(botClient, message, cancellationToken);
            }
        }
    }

    private async Task HandlePrivateMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        // Check if admin is sending media (photo/video/document) as ad
        if (message.From?.Id == _settings.BotConfiguration.AdminUserId)
        {
            // Handle photo with caption as advertisement
            if (message.Photo != null && !string.IsNullOrEmpty(message.Caption))
            {
                await HandleAddMediaAdAsync(botClient, message, "photo", message.Photo.Last().FileId, cancellationToken);
                return;
            }

            // Handle video with caption as advertisement
            if (message.Video != null && !string.IsNullOrEmpty(message.Caption))
            {
                await HandleAddMediaAdAsync(botClient, message, "video", message.Video.FileId, cancellationToken);
                return;
            }

            // Handle document with caption as advertisement
            if (message.Document != null && !string.IsNullOrEmpty(message.Caption))
            {
                await HandleAddMediaAdAsync(botClient, message, "document", message.Document.FileId, cancellationToken);
                return;
            }
        }

        if (message.Text == null)
            return;

        if (message.From?.Id != _settings.BotConfiguration.AdminUserId)
        {
            string welcomeMessage = """
                                        Salom! 👋 Men sizning guruhlaringiz uchun maxsus **reklamasiz botman**.

                                        Meni guruhga qo'shing va admin huquqlarini bering — shunda men quyidagilarni qilaman:

                                        🚫 Har qanday reklama va spam xabarlarini avtomatik aniqlayman va o'chirishim mumkin;

                                        🌐 Krilcha, lotincha, ruscha va boshqa tillarda yozilgan reklamalarni ham sezaman;

                                        🔒 Guruhdagi tartibni saqlayman va foydalanuvchilarni bezovta qiluvchi xabarlarni bloklayman;

                                        ⚡ Xabarlar tez va samarali tarzda filtrlash orqali guruhingiz toza va qulay bo'lishini ta'minlayman.

                                        Guruhingizni tartibli va qiziqarli qilishni xohlaysizmi? Shunda darhol meni admin qiling va ishga tushiring! 🚀
                                        """;

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: welcomeMessage,
                cancellationToken: cancellationToken
            );

            return;
        }


        // Admin commands
        await HandleAdminCommandAsync(botClient, message, cancellationToken);
    }

    private async Task HandleGroupAdminCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var command = message.Text?.ToLower().Trim();

        switch (command)
        {
            case "/start":
            case "/help":
                await SendGroupHelpMessageAsync(botClient, message.Chat.Id, cancellationToken);
                break;

            case "/stats":
            case "/statistics":
                await SendGroupStatisticsAsync(botClient, message.Chat.Id, cancellationToken);
                break;

            case var cmd when cmd?.StartsWith("/setadinterval ") == true:
                await HandleSetAdIntervalAsync(botClient, message, cancellationToken);
                break;

            case "/togglegroupads":
                await HandleToggleGroupAdsAsync(botClient, message, cancellationToken);
                break;
        }
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
                // If in private chat or group, show appropriate stats
                if (message.Chat.Type == ChatType.Private)
                {
                    await SendStatisticsAsync(botClient, message.Chat.Id, cancellationToken);
                }
                else
                {
                    await SendGroupStatisticsAsync(botClient, message.Chat.Id, cancellationToken);
                }
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
/addad <matn> - Matnli reklama qo'shish
🖼️ Rasm yuboring + caption - Rasmli reklama qo'shish
/listads - Barcha reklamalar ro'yxati
/deletead <id> - Reklamani o'chirish
/togglead <id> - Reklamani yoqish/o'chirish
/adstats - Reklama statistikasi
/setadinterval <daqiqa> - Guruh uchun interval o'rnatish
/togglegroupads - Guruhda reklamalarni yoqish/o'chirish

💡 Rasmli reklama qo'shish:
1. Botga rasm yuboring
2. Caption'da reklama matnini yozing
3. Tayyor! Bot avtomatik qo'shadi

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

        if (!allStats.Any())
        {
            await botClient.SendTextMessageAsync(
                chatId,
                "Hozircha statistika yo'q.",
                cancellationToken: cancellationToken
            );
            return;
        }

        var statsText = "📊 Bot Statistikasi (Barcha Guruhlar):\n\n";

        foreach (var stat in allStats)
        {
            // Try to get the most up-to-date group title
            var group = _databaseService.GetGroup(stat.ChatId);
            var groupTitle = group?.Title ?? stat.ChatTitle ?? $"Chat {stat.ChatId}";

            var statusEmoji = group?.IsActive == true ? "✅" : "❌";

            statsText += $"{statusEmoji} {groupTitle}\n";
            statsText += $"   ID: {stat.ChatId}\n";
            statsText += $"   👋 Kirish: {stat.DeletedJoinMessages} | Chiqish: {stat.DeletedLeaveMessages}\n";
            statsText += $"   🚫 Spam: {stat.DeletedSpamMessages}\n";
            statsText += $"   ✅ Jami: {stat.DeletedJoinMessages + stat.DeletedLeaveMessages + stat.DeletedSpamMessages}\n\n";
        }

        var totalDeleted = allStats.Sum(s => s.DeletedJoinMessages + s.DeletedLeaveMessages + s.DeletedSpamMessages);
        statsText += $"🎯 Umumiy o'chirilgan xabarlar: {totalDeleted}\n";
        statsText += $"📁 Jami guruhlar: {allStats.Count}";

        await botClient.SendTextMessageAsync(
            chatId,
            statsText,
            cancellationToken: cancellationToken
        );
    }

    private async Task SendGroupsListAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var groups = _databaseService.GetAllGroupsIncludingInactive();

        if (!groups.Any())
        {
            await botClient.SendTextMessageAsync(
                chatId,
                "Bot hozircha hech qanday guruhda emas.",
                cancellationToken: cancellationToken
            );
            return;
        }

        var activeGroups = groups.Where(g => g.IsActive).ToList();
        var inactiveGroups = groups.Where(g => !g.IsActive).ToList();

        var groupsText = $"📁 Bot Guruhlari ({groups.Count} ta):\n\n";

        // Active groups
        if (activeGroups.Any())
        {
            groupsText += "✅ AKTIV GURUHLAR:\n\n";
            foreach (var group in activeGroups.OrderByDescending(g => g.JoinedAt))
            {
                groupsText += $"• {group.Title ?? "Noma'lum"}\n";
                groupsText += $"  ID: {group.ChatId}\n";
                groupsText += $"  Qo'shildi: {group.JoinedAt:dd.MM.yyyy HH:mm}\n\n";
            }
        }

        // Inactive groups
        if (inactiveGroups.Any())
        {
            groupsText += "❌ NOAKTIV GURUHLAR (Bot o'chirilgan):\n\n";
            foreach (var group in inactiveGroups.OrderByDescending(g => g.JoinedAt))
            {
                groupsText += $"• {group.Title ?? "Noma'lum"}\n";
                groupsText += $"  ID: {group.ChatId}\n";
                groupsText += $"  Qo'shildi: {group.JoinedAt:dd.MM.yyyy HH:mm}\n\n";
            }
        }

        groupsText += $"📊 Aktiv: {activeGroups.Count} | Noaktiv: {inactiveGroups.Count}";

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
                "📭 Hozircha reklamalar yo'q.\n\nYangi reklama qo'shish:\n• Matn: /addad <matn>\n• Rasm: Rasm yuboring + caption yozing",
                cancellationToken: cancellationToken
            );
            return;
        }

        var adsText = "📢 Reklamalar ro'yxati:\n\n";

        foreach (var ad in ads)
        {
            var status = ad.IsActive ? "✅ Aktiv" : "❌ O'chirilgan";
            var mediaIcon = !string.IsNullOrEmpty(ad.MediaType) ? GetMediaIcon(ad.MediaType) : "📝";

            adsText += $"ID: {ad.Id} - {status}\n";
            adsText += $"{mediaIcon} {(ad.Text.Length > 50 ? ad.Text.Substring(0, 50) + "..." : ad.Text)}\n";
            if (!string.IsNullOrEmpty(ad.MediaType))
            {
                adsText += $"🎬 Turi: {ad.MediaType}\n";
            }
            adsText += $"📅 Yaratildi: {ad.CreatedAt:dd.MM.yyyy HH:mm}\n\n";
        }

        await botClient.SendTextMessageAsync(
            chatId,
            adsText,
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleAddMediaAdAsync(ITelegramBotClient botClient, Message message, string mediaType, string fileId, CancellationToken cancellationToken)
    {
        var caption = message.Caption ?? "";

        if (string.IsNullOrWhiteSpace(caption))
        {
            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                "❌ Rasm uchun caption (matn) yozing!\n\nMisol: Rasm yuboring va caption'da reklamangizni yozing.",
                cancellationToken: cancellationToken
            );
            return;
        }

        _databaseService.AddAdvertisementWithMedia(caption, mediaType, fileId);

        var mediaIcon = GetMediaIcon(mediaType);
        await botClient.SendTextMessageAsync(
            message.Chat.Id,
            $"✅ {mediaIcon} Rasmli reklama qo'shildi!\n\n📝 Matn: {caption}\n🎬 Turi: {mediaType}",
            cancellationToken: cancellationToken
        );
    }

    private string GetMediaIcon(string mediaType)
    {
        return mediaType?.ToLower() switch
        {
            "photo" => "🖼️",
            "video" => "🎥",
            "document" => "📄",
            _ => "📝"
        };
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
        var chatId = chatMember.Chat.Id;
        var chatTitle = chatMember.Chat.Title ?? "Unknown Group";

        // Ensure group is tracked (in case it wasn't added before)
        _databaseService.AddOrUpdateGroup(chatId, chatTitle);

        // Track when users join via this event too
        if (chatMember.NewChatMember.Status == ChatMemberStatus.Member ||
            chatMember.NewChatMember.Status == ChatMemberStatus.Restricted)
        {
            _databaseService.TrackUserJoin(chatMember.NewChatMember.User.Id, chatId);
        }
    }

    private async Task SendGroupHelpMessageAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var helpText = @"🤖 SmartBot - Guruh Uchun Yordam

📊 Mavjud Buyruqlar:
/help - Yordam
/stats - Bu guruh statistikasi

🔧 Sozlashlar (Guruh Adminlari):
/setadinterval <daqiqa> - Reklama intervali
/togglegroupads - Reklamalarni yoqish/o'chirish

ℹ️ Bot avtomatik ravishda:
✅ Kirish/Chiqish xabarlarini o'chiradi
✅ Spam xabarlarini bloklaydi
✅ Statistika yig'adi";

        await botClient.SendTextMessageAsync(
            chatId,
            helpText,
            cancellationToken: cancellationToken
        );
    }

    private async Task SendGroupStatisticsAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var stat = _databaseService.GetStatistics(chatId);
        var group = _databaseService.GetGroup(chatId);
        var groupTitle = group?.Title ?? stat.ChatTitle ?? "Bu guruh";

        if (stat.DeletedJoinMessages == 0 && stat.DeletedLeaveMessages == 0 && stat.DeletedSpamMessages == 0)
        {
            await botClient.SendTextMessageAsync(
                chatId,
                $"📊 {groupTitle}\n\nHozircha statistika yo'q.",
                cancellationToken: cancellationToken
            );
            return;
        }

        var statsText = $"📊 {groupTitle} - Statistika\n\n";
        statsText += $"ID: {chatId}\n\n";
        statsText += $"👋 Kirish xabarlari: {stat.DeletedJoinMessages}\n";
        statsText += $"👋 Chiqish xabarlari: {stat.DeletedLeaveMessages}\n";
        statsText += $"🚫 Spam xabarlari: {stat.DeletedSpamMessages}\n\n";

        var total = stat.DeletedJoinMessages + stat.DeletedLeaveMessages + stat.DeletedSpamMessages;
        statsText += $"✅ Jami o'chirilgan: {total}";

        await botClient.SendTextMessageAsync(
            chatId,
            statsText,
            cancellationToken: cancellationToken
        );
    }

    private async Task<bool> IsUserGroupAdmin(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        try
        {
            var chatMember = await botClient.GetChatMemberAsync(chatId, userId, cancellationToken);
            return chatMember.Status == ChatMemberStatus.Administrator ||
                   chatMember.Status == ChatMemberStatus.Creator;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} is admin in chat {ChatId}", userId, chatId);
            return false;
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
