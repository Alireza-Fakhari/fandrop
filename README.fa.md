<div dir="rtl">

# FanDrop

[English](README.md) | **فارسی**

<p align="center"><a href="https://github.com/Alireza-Fakhari/fandrop/releases/latest/download/FanDrop-Setup.exe"><img src="https://img.shields.io/badge/%D8%AF%D8%A7%D9%86%D9%84%D9%88%D8%AF-FanDrop%20%D8%A8%D8%B1%D8%A7%DB%8C%20%D9%88%DB%8C%D9%86%D8%AF%D9%88%D8%B2-2563eb?style=for-the-badge&logo=windows&logoColor=white" alt="دانلود FanDrop برای ویندوز" height="48"></a></p>

فایل را بکشید، یک کلید نگه دارید، روی یکی از گزینه‌ها رها کنید — تمام.
FanDrop یک برنامهٔ کوچک ویندوزی است که در System Tray اجرا می‌شود و هنگام کشیدن (Drag) فایل از File Explorer، یک منوی دایره‌ای (بادبزنی) از کارهای سریع زیر نشانگر ماوس نشان می‌دهد. بدون باز کردن هیچ برنامهٔ دیگری فایل‌ها را تبدیل، فشرده، کوچک، ZIP یا حذف کنید.

![اسکرین‌شات FanDrop](docs/screenshots/fan.png)

## امکانات

| کلید هنگام کشیدن | منو | نمونه‌ها |
|---|---|---|
| **Shift** | تبدیل فرمت | تصویر ← PNG / JPG / WEBP / BMP / ICO · ویدیو ← MP4 / MOV / WEBM / GIF / MP3 · صوت ← MP3 / M4A / WAV / FLAC / OGG |
| **Ctrl** | عملیات | تغییر اندازه، فشرده‌سازی، چرخش، سیاه‌وسفید، حذف متادیتا · بی‌صدا کردن، برش ۳۰ ثانیه، استخراج فریم · نرمال‌سازی صدا، مونو |
| **Alt** | ابزارها | کپی مسیر، ZIP، نمایش در Explorer، باز کردن، انتقال به سطل بازیافت |

- منوی شیشه‌ای مات با پیش‌نمایش زندهٔ فایل
- پشتیبانی از چند فایل هم‌زمان
- خروجی‌ها در پوشهٔ `Downloads\FanDrop` ذخیره و در Explorer نمایش داده می‌شوند
- هیچ فایلی بازنویسی نمی‌شود — فایل تکراری شماره می‌گیرد
- اجرا در System Tray و امکان اجرای خودکار با ویندوز

## نصب

۱. فایل **`FanDrop-Setup.exe`** را از [آخرین نسخه](https://github.com/Alireza-Fakhari/fandrop/releases/latest/download/FanDrop-Setup.exe) دانلود کنید.
۲. آن را اجرا کنید (نیازی به دسترسی Admin نیست). FFmpeg همراه نصاب است.
۳. FanDrop در System Tray (گوشهٔ پایین‌راست نوار وظیفه) اجرا می‌شود.

> چون نصاب امضای دیجیتال ندارد، ممکن است Windows SmartScreen هشدار دهد. روی **More info ← Run anyway** کلیک کنید.

## نحوهٔ استفاده

۱. شروع به کشیدن یک فایل در Explorer کنید.
۲. در حین کشیدن، **Shift**، **Ctrl** یا **Alt** را نگه دارید — منو زیر نشانگر ظاهر می‌شود.
۳. نشانگر را روی گزینهٔ دلخواه ببرید و کلید ماوس را رها کنید.
۴. نتیجه در `Downloads\FanDrop` باز می‌شود.

با راست‌کلیک روی آیکون Tray به گزینه‌های **How to use**، **Open output folder**، **Open log** و **Quit** دسترسی دارید.

## پیش‌نیازها

- ویندوز ۱۰ یا ۱۱ (۶۴ بیتی)
- FFmpeg برای تبدیل فایل‌های چندرسانه‌ای (در نصاب موجود است؛ برای اجرا از سورس با `winget install Gyan.FFmpeg` نصب کنید)

## ساخت از سورس

```bash
git clone https://github.com/Alireza-Fakhari/fandrop.git
cd fandrop/FanDropWin
dotnet run -c Release
```

به [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) نیاز دارد. جزئیات ساخت نصاب در [CONTRIBUTING.md](CONTRIBUTING.md) آمده است.

## مشارکت

گزارش باگ و Pull Request با کمال میل پذیرفته می‌شود — [CONTRIBUTING.md](CONTRIBUTING.md) را ببینید.

## مجوز

[MIT](LICENSE) © علیرضا فخاری.
نصاب شامل [FFmpeg](https://ffmpeg.org/) است که مجوز جداگانهٔ LGPL/GPL دارد.

</div>
