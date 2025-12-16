# 🖼️ Rasmli Reklama - Qisqa Qo'llanma

## ✨ Yangi Imkoniyat!

Endi reklamalaringizga **rasm**, **video** yoki **fayl** qo'shishingiz mumkin!

---

## 🚀 Qanday Ishlaydi?

### 1. **Rasm yuboring**
Botga shaxsiy chatda istalgan rasm yuboring.

### 2. **Caption yozing**
Rasmga caption (izoh) qo'shing - bu sizning reklama matningiz bo'ladi.

### 3. **Tayyor!**
Bot avtomatik reklamani qo'shadi va barcha guruhlarga yuboradi!

---

## 📱 Misol

### Telegram'da:

1. Botni oching (shaxsiy chat)
2. Mahsulot rasmini yuboring
3. Rasmga caption qo'shing:

```
🎉 Yangi mahsulotimiz!

✅ 50% chegirma
✅ Tez yetkazib berish
✅ Kafolat beriladі

📞 Bog'lanish: @yourusername
```

4. Yuborish tugmasini bosing

### Bot javobi:

```
✅ 🖼️ Rasmli reklama qo'shildi!

📝 Matn: 🎉 Yangi mahsulotimiz!...
🎬 Turi: photo
```

---

## 🎬 Qo'llab-quvvatlanadigan Formatlar

| Turi | Icon | Tavsif |
|------|------|--------|
| **Photo** | 🖼️ | Rasm (PNG, JPG, JPEG) |
| **Video** | 🎥 | Video (MP4, MOV) |
| **Document** | 📄 | Har qanday fayl (PDF, ZIP, etc.) |

---

## ✅ Qoidalar

1. **Caption majburiy** - Bo'sh caption bilan rasm yuborib bo'lmaydi
2. **Faqat admin** - Faqat bot egasi reklama qo'sha oladi
3. **Shaxsiy chat** - Faqat botga shaxsiy chatda yuboring (guruhda emas)
4. **Avtomatik** - Bot rasmni o'zi saqlaydi, siz nusxa olishingiz shart emas

---

## 📋 Ko'rish va Boshqarish

### Barcha reklamalarni ko'rish:
```
/listads
```

**Natija:**
```
📢 Reklamalar ro'yxati:

ID: 1 - ✅ Aktiv
🖼️ 🎉 Yangi mahsulotimiz! 50% chegirma...
🎬 Turi: photo
📅 Yaratildi: 12.12.2024 10:30

ID: 2 - ✅ Aktiv
📝 Matnli reklama
📅 Yaratildi: 12.12.2024 09:15
```

### Reklamani o'chirish:
```
/deletead 1
```

### Reklamani pause qilish:
```
/togglead 1
```

---

## 💡 Maslahatlar

### ✅ Yaxshi Amaliyot:

- **Sifatli rasm ishlating** - Yorqin va tushunarli
- **Qisqa matn** - 2-3 qator yetarli
- **Emoji qo'shing** - Ko'proq diqqat tortadi
- **CTA (Call-to-Action)** - "Hoziroq xarid qiling!", "Bog'laning!"

### ❌ Qilmang:

- Bo'sh caption bilan rasm yuborish
- Juda uzun matn (1000+ belgi)
- Sifatsiz yoki blur rasmlar
- Spam ko'rinishdagi kontentlar

---

## 🎯 To'liq Ssenariy

```
1. Bot oching (@yourbotusername)
2. Rasm yuboring
3. Caption:

   🎁 BLACK FRIDAY CHEGIRMASI!

   ⚡ Faqat 3 kun!
   ⚡ 70% gacha chegirma!
   ⚡ Bepul yetkazish!

   🛒 Xarid: t.me/yourshop

4. Yuborish
5. ✅ Reklama qo'shildi!
```

Bot avtomatik ravishda:
- Rasmni saqlaydi
- Har 60 daqiqada (yoki sizning intervalingiz)
- Barcha aktiv guruhlarga yuboradi
- Rasm + caption ko'rinishida

---

## ⚙️ Sozlamalar

### Rasmli reklama uchun maxsus sozlash kerak emas!

Barcha sozlamalar odatdagi reklama bilan bir xil:
- Interval: `appsettings.json` → `DefaultIntervalMinutes`
- Yoqish/o'chirish: `/togglegroupads`
- Guruh uchun: `/setadinterval 120`

---

## 🆘 Muammolar?

### Rasm qo'shilmayapti?

**Tekshiring:**
1. ✅ Siz bot egasi (admin)misiz?
2. ✅ Botga shaxsiy chatda yuborayapsizmi?
3. ✅ Caption yozdingizmi?
4. ✅ Bot ishlab turibdimi?

### Caption yo'q xatosi?

**Yechim:**
- Rasmni yuborishdan **oldin** yoki **yuborishda** caption qo'shing
- Telegram'da: Rasm tanlang → Pastdagi "Add a caption..." → Matn yozing → Yuborish

---

## 🎊 Tayyor!

Endi sizning reklamalaringiz **yanada jozibador** va **samarali**! 🚀

**Rasmli reklama** = **Ko'proq diqqat** = **Ko'proq natija**

📖 **To'liq qo'llanma**: [REKLAMA_QOSHISH.md](REKLAMA_QOSHISH.md)
