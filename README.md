# 🤖 SmartBot - Professional Telegram Group Moderator

Professional Telegram bot guruhlarni boshqarish va moderatsiya qilish uchun.

## ✨ Funksiyalar

### 🔥 Asosiy funksiyalar:
- ✅ **Avtomatik tozalash**: Guruhga kirish/chiqish xabarlarini darhol o'chiradi
- 🛡️ **Spam himoyasi**: Reklama va spam xabarlarni aniqlaydi va o'chiradi
- 👥 **Yangi userlar nazorati**: Yangi a'zolarning URL yuborishini cheklaydi (30 daqiqa)
- 📊 **Batafsil statistika**: Har bir guruh uchun alohida statistika va hisobotlar
- 🗄️ **Ma'lumotlar bazasi**: LiteDB yordamida barcha ma'lumotlarni xavfsiz saqlaydi
- 📢 **Reklama tizimi** _(opsional)_: Avtomatik reklama yuborish imkoniyati (sozlanadi)

### 🎯 Spam aniqlash mezonlari:
- **Kalit so'zlar**: 7+ spam kalit so'zlar asosida aniqlash (sozlanadi)
- **URL cheklash**: Xabardagi maksimal URL soni (default: 2 ta)
- **Yangi userlar**: 30 daqiqa ichida qo'shilganlar URL yubora olmaydi
- **Caption tekshirish**: Rasmli xabarlardagi yozuvlarni ham tekshiradi
- **To'liq sozlanishi**: Barcha parametrlarni o'zgartirish mumkin

## 🚀 O'rnatish

### 1. Bot yaratish

Telegram'da [@BotFather](https://t.me/BotFather) ga murojaat qiling:

```
/newbot
```

Bot nomi va username ni kiriting, keyin token ni oling.

### 2. Konfiguratsiya

`SmartBot/appsettings.json` faylini tahrirlang:

```json
{
  "BotConfiguration": {
    "BotToken": "YOUR_BOT_TOKEN_HERE",  // BotFather dan olgan tokeningiz
    "AdminUserId": 0  // Sizning Telegram User ID (opsional)
  }
}
```

**User ID ni bilish:**
- [@userinfobot](https://t.me/userinfobot) ga `/start` yuboring
- Sizga User ID ni ko'rsatadi

### 3. Ishga tushirish

```bash
cd SmartBot
dotnet run
```

Yoki build qilib ishlatish:

```bash
dotnet build -c Release
cd bin/Release/net8.0
./SmartBot
```

### 4. Guruhga qo'shish

1. Botni guruhga qo'shing
2. Botni **admin** qiling
3. **"Delete messages"** huquqini bering

Bot guruhda quyidagi xabarlarni o'chiradi:
- Foydalanuvchi guruhga qo'shildi
- Foydalanuvchi guruhdan chiqdi
- Spam va reklama xabarlari

## 📋 Admin buyruqlari

Bot bilan shaxsiy chatda yoki guruhda (agar siz admin bo'lsangiz):

### 👮 Asosiy buyruqlar:
- `/help` - Yordam va bot haqida ma'lumot
- `/stats` - Barcha guruhlar bo'yicha batafsil statistika
- `/groups` - Bot qaysi guruhlarda ekanligini ko'rish

### 📢 Reklama buyruqlari (opsional):
- `/addad <matn>` - Yangi reklama qo'shish
- `/listads` - Barcha reklamalar ro'yxati
- `/deletead <id>` - Reklamani o'chirish
- `/togglead <id>` - Reklamani yoqish/o'chirish
- `/adstats` - Reklama statistikasi
- `/setadinterval <daqiqa>` - Guruh uchun interval sozlash
- `/togglegroupads` - Guruhda reklamalarni yoqish/o'chirish

> 💡 **Eslatma**: Reklama tizimi default holatda **o'chirilgan**. Yoqish uchun `appsettings.json` da `"Advertising": { "Enabled": true }` qiling.

## ⚙️ Sozlamalar

`appsettings.json` faylida barcha sozlamalar:

### Funksiyalarni yoqish/o'chirish:
```json
"Features": {
  "AutoDeleteJoinLeaveMessages": true,  // Kirish/chiqish xabarlarini o'chirish
  "SpamDetection": true,                // Spam aniqlash
  "EnableStatistics": true              // Statistika yig'ish
}
```

### Spam aniqlash parametrlari:
```json
"SpamDetection": {
  "Keywords": [                         // Spam kalit so'zlar
    "reklama",
    "telegram kanal",
    "kanalga obuna",
    "pul ishlang",
    "crypto",
    "bitcoin",
    "forex"
  ],
  "MaxUrlsPerMessage": 2,               // Xabardagi maksimal URL soni
  "BlockNewUsersWithUrls": true,        // Yangi userlarni URL bilan bloklash
  "NewUserTimeWindowMinutes": 30        // Yangi user hisoblanadigan vaqt (daqiqa)
}
```

### Reklama tizimi sozlamalari (opsional):
```json
"Advertising": {
  "Enabled": false,                     // Reklama tizimini yoqish/o'chirish
  "DefaultIntervalMinutes": 60,         // Reklamalar orasidagi interval (daqiqa)
  "AutoStartOnBotStartup": true         // Bot ishga tushganda avtomatik boshlash
}
```

**Qanday ishlaydi:**
- Bot har 60 daqiqada (yoki o'zingiz belgilagan intervarda) reklama yuboradi
- Har bir guruh uchun alohida interval o'rnatish mumkin: `/setadinterval 30`
- Bir nechta reklama qo'shsangiz, ular ketma-ket yuboriladi (rotatsiya)
- Guruhda o'chirish/yoqish: `/togglegroupads`

## 🗄️ Ma'lumotlar bazasi

Bot **LiteDB** (NoSQL) ma'lumotlar bazasidan foydalanadi. Barcha ma'lumotlar `smartbot.db` faylida saqlanadi.

### Saqlanadigan ma'lumotlar:
- Guruhlar ro'yxati va ma'lumotlari
- Foydalanuvchilar qachon guruhga qo'shilganligi
- Har bir guruh uchun statistika:
  - O'chirilgan kirish xabarlari
  - O'chirilgan chiqish xabarlari
  - O'chirilgan spam xabarlari

## 🛡️ Xavfsizlik

Bot quyidagi xavfsizlik choralarini qo'llaydi:

- ✅ Faqat admin foydalanuvchi maxsus buyruqlarni bajarishi mumkin
- ✅ Bot faqat kerakli huquqlarni talab qiladi
- ✅ Barcha xatolar log'lanadi
- ✅ Ma'lumotlar lokal saqlanadi (privacy)

## 📊 Statistika namunasi

```
📊 Bot Statistikasi:

📍 UzDev Community
   👋 Kirish xabarlari: 234
   👋 Chiqish xabarlari: 89
   🚫 Spam xabarlari: 45
   ✅ Jami: 368

📍 IT Dasturchilar
   👋 Kirish xabarlari: 567
   👋 Chiqish xabarlari: 123
   🚫 Spam xabarlari: 78
   ✅ Jami: 768

🎯 Umumiy o'chirilgan xabarlar: 1136
```

## 🔧 Texnologiyalar

- **.NET 8.0** - Backend framework
- **Telegram.Bot** - Telegram Bot API kutubxonasi
- **LiteDB** - NoSQL ma'lumotlar bazasi
- **Microsoft.Extensions** - Dependency Injection, Configuration, Logging

## 📝 Litsenziya

MIT License - batafsil [LICENSE](LICENSE) faylida.

## 🤝 Hissa qo'shish

Pull requestlar qabul qilinadi! Iltimos:
1. Fork qiling
2. Feature branch yarating (`git checkout -b feature/AmazingFeature`)
3. Commit qiling (`git commit -m 'Add some AmazingFeature'`)
4. Push qiling (`git push origin feature/AmazingFeature`)
5. Pull Request oching

## 💬 Muammolar

Agar muammo yoki taklifingiz bo'lsa, [Issues](../../issues) bo'limida yozing.

## 📞 Aloqa

Savollaringiz bo'lsa, Issues orqali yozing yoki Pull Request yuboring.

---

**⭐ Agar loyiha foydali bo'lsa, star bosishni unutmang!**
