# 📢 Reklama Tizimi - Foydalanish Qo'llanmasi

## 🎯 Reklama Tizimi Nima?

SmartBot guruhlaringizga avtomatik ravishda reklama yuborish imkoniyatini beradi. Siz reklamalarni qo'shasiz, bot esa belgilangan vaqt oralig'ida barcha guruhlarga avtomatik yuboradi.

---

## ⚙️ Sozlashlar

### 1. Reklama Tizimini Yoqish

`appsettings.json` faylida:
```json
"Advertising": {
    "Enabled": true,              // true - yoqilgan, false - o'chirilgan
    "DefaultIntervalMinutes": 60, // Har 60 daqiqada (1 soatda) yuboriladi
    "AutoStartOnBotStartup": true // Bot ishga tushganda avtomatik boshlanadi
}
```

### 2. Interval o'zgartirish

- `DefaultIntervalMinutes: 30` - Har 30 daqiqada
- `DefaultIntervalMinutes: 60` - Har 1 soatda
- `DefaultIntervalMinutes: 120` - Har 2 soatda
- `DefaultIntervalMinutes: 1440` - Kuniga 1 marta

---

## 📝 Reklama Qo'shish

### 1️⃣ Matnli Reklama

Botga shaxsiy chatda yuboring:

```
/addad Bu yerda reklama matni!
```

**Misol:**
```
/addad 🎉 Maxsus taklif! Bizning yangi mahsulotimizga 50% chegirma!
```

**Natija:**
```
✅ Reklama qo'shildi!

📝 Matn: 🎉 Maxsus taklif! Bizning yangi mahsulotimizga 50% chegirma!
```

---

### 2️⃣ Rasmli Reklama 🖼️

Rasmli reklama qo'shish juda oson!

**Qadamlar:**

1. **Botga rasm yuboring** (photo, video, yoki document)
2. **Caption'da reklama matnini yozing**

**Misol:**

1. Botga mahsulot rasmini yuboring
2. Caption'da yozing:
```
🎉 Yangi mahsulotimiz! 50% chegirma!

📞 Bog'lanish: @username
```

**Natija:**
```
✅ 🖼️ Rasmli reklama qo'shildi!

📝 Matn: 🎉 Yangi mahsulotimiz! 50% chegirma!...
🎬 Turi: photo
```

**Qo'llab-quvvatlanadi:**
- 🖼️ **Photo** (rasm)
- 🎥 **Video** (video)
- 📄 **Document** (fayl)

**Muhim:**
- Caption bo'sh bo'lmasligi kerak
- Rasm yuborishdan oldin caption yozish shart emas, rasm yuborishda caption qo'shing
- Bot rasmni saqlaydi va avtomatik barcha guruhlarga yuboradi

---

## 📋 Barcha Reklamalarni Ko'rish

```
/listads
```

**Natija:**
```
📢 Reklamalar ro'yxati:

ID: 1 - ✅ Aktiv
📝 🎉 Maxsus taklif! Bizning yangi mahsulotimizga 50% chegirma!
📅 Yaratildi: 12.12.2024 10:30

ID: 2 - ✅ Aktiv
📝 📚 Yangi kurslarimizga yoziling!
📅 Yaratildi: 12.12.2024 11:00
```

---

## 🎛️ Reklamani Boshqarish

### Reklamani o'chirish (o'chirib tashlash emas, faqat pause):
```
/togglead 1
```

**Natija:**
```
✅ Reklama o'chirildi!

📝 Matn: 🎉 Maxsus taklif!...
```

### Reklamani qayta yoqish:
```
/togglead 1
```

**Natija:**
```
✅ Reklama yoqildi!
```

### Reklamani butunlay o'chirib tashlash:
```
/deletead 1
```

**Natija:**
```
✅ Reklama o'chirildi!

📝 Matn: 🎉 Maxsus taklif!...
```

---

## 📊 Reklama Statistikasi

```
/adstats
```

**Natija:**
```
📊 Reklama Statistikasi:

📢 Reklama #1
📝 🎉 Maxsus taklif! Bizning yangi mahsulotimizga...
📨 Jami yuborildi: 45
🏘️ Guruhlar: 3

📢 Reklama #2
📝 📚 Yangi kurslarimizga yoziling!
📨 Jami yuborildi: 30
🏘️ Guruhlar: 3

🎯 Umumiy yuborilgan: 75
```

---

## 🔧 Guruh uchun Maxsus Sozlamalar

### Guruhda interval o'zgartirish (guruh adminlari):

Guruhda yozasiz:
```
/setadinterval 120
```

**Natija:**
```
✅ Reklama intervali 120 daqiqaga o'rnatildi!
```

Bu guruhda **faqat** har 2 soatda reklama yuboriladi (boshqa guruhlarda standart interval bo'yicha).

### Guruhda reklamalarni o'chirish:

```
/togglegroupads
```

**Natija:**
```
✅ Guruhda reklamalar o'chirildi!
```

Qayta yoqish uchun yana bir marta yuboring:
```
/togglegroupads
```

**Natija:**
```
✅ Guruhda reklamalar yoqildi!
```

---

## 🚀 Qanday Ishlaydi?

### 1️⃣ **Botni ishga tushiring:**
```bash
dotnet run
```

### 2️⃣ **Reklama qo'shing:**
```
/addad 🎉 Bizning yangi xizmatimiz!
```

### 3️⃣ **Bot avtomatik ishlay boshlaydi:**
- Har 60 daqiqada (yoki siz belgilagan interval)
- Bot barcha aktiv guruhlarni tekshiradi
- Agar guruhda reklama yuborish vaqti kelgan bo'lsa
- Keyingi reklamani yuboradi
- Statistikani yangilaydi

### 4️⃣ **Reklama tartibi:**
Agar sizda 3 ta reklama bo'lsa:
- Guruh A: Reklama 1 → Reklama 2 → Reklama 3 → Reklama 1...
- Guruh B: Reklama 1 → Reklama 2 → Reklama 3 → Reklama 1...

Har bir guruh o'z navbatini saqlab boradi!

---

## ⏱️ Vaqt Jadvali Misoli

**Sozlamalar:**
- Reklama intervali: 60 daqiqa
- 3 ta reklama bor

**Guruhda:**
- 10:00 - Reklama #1 yuborildi
- 11:00 - Reklama #2 yuborildi
- 12:00 - Reklama #3 yuborildi
- 13:00 - Reklama #1 yuborildi (qayta takrorlanadi)
- ...

---

## ⚠️ Muhim Eslatmalar

1. **Bot admin bo'lishi kerak** - Guruhda xabar yuborish uchun
2. **Guruh aktiv bo'lishi kerak** - Faqat aktiv guruhlarga yuboriladi
3. **Kamida 1 ta aktiv reklama bo'lishi kerak**
4. **Interval juda qisqa bo'lmasin** - Spam deb hisoblanishi mumkin
5. **Reklama mazmuni mos bo'lsin** - Telegram qoidalariga rioya qiling

---

## 🛠️ Muammolar va Yechimlar

### ❓ Reklama yuborilmayapti?

**Tekshiring:**
1. ✅ `appsettings.json`'da `Enabled: true` mi?
2. ✅ Bot ishlab turibdimi?
3. ✅ Kamida 1 ta aktiv reklama bormi? (`/listads`)
4. ✅ Guruhda reklamalar yoqilganmi? (`/togglegroupads`)
5. ✅ Bot guruhda admin va xabar yuborish huquqi bormi?

### ❓ Juda ko'p reklama yuborilmoqda?

**Interval oshiring:**
```json
"DefaultIntervalMinutes": 120  // 2 soat
```

Yoki guruhda:
```
/setadinterval 180  // 3 soat
```

### ❓ Faqat ba'zi guruhlarga yuborilsin?

**Kerak bo'lmagan guruhlarda:**
```
/togglegroupads
```

---

## 📞 Yordam

Muammo yuzaga kelsa:
1. Bot loglarini tekshiring
2. `/adstats` - statistikani ko'ring
3. `/groups` - guruhlar ro'yxatini tekshiring
4. Bot qayta ishga tushiring: `Ctrl+C` → `dotnet run`

---

## 🎉 Tayyor!

Endi botingiz avtomatik ravishda reklamalarni yuboradi! 🚀
