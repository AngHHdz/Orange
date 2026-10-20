# 🍊 Orange

**Offline file converter and phone sharing for Windows.**
Drag a file, press a key, drop it on the wheel. Everything happens on your PC: nothing is uploaded to the internet.

## How it works

Start dragging any file (or several) and press:

| Key | Mode | Examples |
|---|---|---|
| **Shift** | **Convert** | HEIC → JPG, MOV → MP4, DOCX → PDF, XLSX → PDF, PDF → DOCX, M4A → MP3 |
| **Ctrl** | **Tools** | Merge / split / compress PDF, OCR, compress video, resize images, ZIP |
| **Alt** | **Actions** | Send to iPhone, copy, copy path, move, float on top, terminal |

A wheel appears under the mouse. Drop the file on a slice; the result is saved next to the original.
No drag? Double-click the tray icon to pick files.

## Features

- **Images**: JPG, PNG, WebP, AVIF, HEIC, GIF, TIFF, BMP, ICO, SVG → PDF; compress, resize, rotate, OCR
- **Video / audio**: MP4, MOV, MKV, WebM, AVI, GIF, MP3, M4A, WAV, FLAC, OGG, Opus; compress, 720p, mute, extract audio
- **Documents**: Word, Excel, PowerPoint, ODT/ODS/ODP, RTF, TXT, HTML, Markdown, EPUB (uses Microsoft Office when installed)
- **PDF**: merge (also with images), split, extract pages, compress, rotate, page numbers, watermark, password protect/unlock, OCR, PDF → Word / images / text / PowerPoint
- **Archives**: ZIP, RAR, 7z, TAR, GZ: extract and repack
- **Subtitles**: SRT ⇄ VTT, to text
- **Phone sharing over Wi-Fi** ("AirDrop" for iPhone ⇄ PC): scan a QR code, add Orange to the Home Screen, send files both ways. Optional iOS Shortcut adds *Send to Orange* to the Share menu.
- **Settings**: English / Español / Português, wheel size, keys, output folder, image quality

## Install

Download `Orange-Setup-<version>.exe` from [Releases](../../releases) and run it.
No admin rights needed; .NET, ffmpeg and Pandoc are included.
Word, Excel and PowerPoint conversions need Microsoft Office.

Requires Windows 10 (2004) or Windows 11, 64-bit.

## Build from source

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download) and [Inno Setup 6](https://jrsoftware.org/isinfo.php)
(`winget install Microsoft.DotNet.SDK.8 JRSoftware.InnoSetup`).

```powershell
.\build.ps1                 # → dist\Orange-Setup-<version>.exe
.\build.ps1 -NoInstaller    # → publish\ (portable folder)
.\build.ps1 -RefreshTools   # re-download the newest ffmpeg and Pandoc
```

For development: `dotnet run --project Orange`.

## Project layout

```
Orange/
  Core/           conversion engines (ffmpeg, Magick.NET, Office COM, Pandoc, PDFsharp, Windows OCR…)
  Core/Share/     phone sharing: local web server + the page the phone sees
  Input/          global mouse/keyboard hooks that detect "dragging + key"
  UI/             wheel, settings, pairing (QR) and dialogs
installer/        Inno Setup script
build.ps1         release build + installer
```

## Third-party software

Orange uses [Magick.NET](https://github.com/dlemstra/Magick.NET) (Apache 2.0),
[PDFsharp](https://www.pdfsharp.com/) 6.2 (MIT), [PdfPig](https://github.com/UglyToad/PdfPig) (Apache 2.0),
[SharpCompress](https://github.com/adamhathcock/sharpcompress) (MIT) and [QRCoder](https://github.com/codebude/QRCoder) (MIT).
The installer bundles [FFmpeg](https://ffmpeg.org/) ([gyan.dev](https://www.gyan.dev/ffmpeg/builds/) build, GPL) and
[Pandoc](https://pandoc.org/) (GPL); their licenses are installed in `tools\licenses`.
