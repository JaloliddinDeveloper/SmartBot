# 🚀 Tezkor Boshlash Qo'llanmasi

SmartBot ni ishga tushirish uchun quyidagi qadamlarni bajaring:

## 1️⃣ Bot Yaratish

1. Telegramda [@BotFather](https://t.me/BotFather) ni oching
2. `/newbot` buyrug'ini yuboring
3. Bot uchun ism kiriting (masalan: "Mening Smart Botim")
4. Bot uchun username kiriting (masalan: "mening_smart_bot")
5. BotFather sizga **Bot Token** beradi. Uni nusxalab oling!

```
Masalan: 1234567890:ABCdefGHIjklMNOpqrsTUVwxyz
```

## 2️⃣ Konfiguratsiya

`SmartBot/appsettings.json` faylini oching va quyidagilarni o'zgartiring:

```json
{
  "BotConfiguration": {
    "BotToken": "SIZNING_BOT_TOKENINGIZ",  // Bu yerga BotFather dan olgan tokenni qo'ying
    "AdminUserId": 123456789                // Bu yerga sizning User ID ingizni qo'ying
  }
}
```

### User ID ni qanday bilish kerak?

1. Telegramda [@userinfobot](https://t.me/userinfobot) ni oching
2. `/start` ni bosing
3. Bot sizga User ID ni ko'rsatadi

## 3️⃣ Botni Ishga Tushirish

### Birinchi usul (Development):
```bash
cd SmartBot
dotnet run
```

### Ikkinchi usul (Production):
```bash
dotnet publish -c Release -o publish
cd publish
./SmartBot
```

## 4️⃣ Guruhga Qo'shish

1. Botni guruhingizga qo'shing (guruhda **Add Members** → bot username ni qidiring)
2. Botni **Administrator** qiling
3. Quyidagi huquqlarni bering:
   - ✅ **Delete messages** (Xabarlarni o'chirish)
   - ✅ **Ban users** (opsional, agar kerak bo'lsa)

## 5️⃣ Test Qilish

Guruhda:
- Biror kishini qo'shing → Bot join xabarni o'chiradi
- Biror kishi chiqib ketsin → Bot leave xabarni o'chiradi
- Spam xabar yuboring → Bot spamni aniqlaydi va o'chiradi

Shaxsiy chatda (faqat admin):
- `/help` - Yordam
- `/stats` - Statistika
- `/groups` - Guruhlar ro'yxati

## ⚙️ Qo'shimcha Sozlamalar

### Spam kalit so'zlarini qo'shish:

`appsettings.json` da:
```json
"SpamDetection": {
  "Keywords": [
    "reklama",
    "telegram kanal",
    "o'zingizning kalit so'zlaringiz"
  ]
}
```

### Funksiyalarni o'chirish:

```json
"Features": {
  "AutoDeleteJoinLeaveMessages": false,  // Join/leave xabarlarni o'chirmaslik
  "SpamDetection": true,
  "EnableStatistics": true
}
```

## 🐛 Muammolar

### "Bot token not configured" xatosi:
- `appsettings.json` da `BotToken` ni to'g'ri kiritganingizni tekshiring

### Bot xabarlarni o'chirmayapti:
- Botga **Admin** huquqlari berganingizni tekshiring
- **Delete messages** huquqi yoqilganligini tekshiring

### Bot javob bermayapti:
- Bot ishga tushganligini konsolda tekshiring
- Internet ulanishini tekshiring
- Bot tokenini qaytadan tekshiring

## 📞 Yordam

Qo'shimcha savol bo'lsa:
1. README.md faylini o'qing
2. GitHub Issues da savol bering

---

**✅ Tayyor! Botingiz endi guruhni tozalab turadi!**
