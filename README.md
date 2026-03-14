# 🎬 Automation Content

A beautiful cross-platform YouTube video downloader built with .NET 9 and Avalonia UI. Works on both **Windows** and **macOS**.

![.NET](https://img.shields.io/badge/.NET-9.0-purple)
![Avalonia](https://img.shields.io/badge/Avalonia-11.2-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS-green)

## ✨ Features

- 📋 **Paste & Preview** — Paste a YouTube URL and instantly see the video title, thumbnail, duration, and view count
- 🎬 **Quality Selection** — Choose from 1080p, 720p, 480p, or Audio Only (MP3)
- 📁 **Custom Save Location** — Pick any folder on your computer to save downloads
- 📥 **Real-time Progress** — Watch the download progress with speed, ETA, and percentage
- ✖ **Cancel Anytime** — Stop a download at any time with the Cancel button
- 📂 **Open Folder** — After download, quickly open the folder containing your file
- 🎨 **Modern Dark UI** — Premium dark theme with purple accents and smooth interactions
- ❌ **Friendly Errors** — Human-readable error messages instead of technical error codes

## 📋 Prerequisites

### 1. yt-dlp (Required)

**macOS:**
```bash
brew install yt-dlp
```

**Windows:**
```bash
winget install yt-dlp
```

Or download from: https://github.com/yt-dlp/yt-dlp/releases

### 2. FFmpeg (Required for video merging & audio extraction)

**macOS:**
```bash
brew install ffmpeg
```

**Windows:**
```bash
winget install ffmpeg
```

### 3. .NET 9 SDK

Download from: https://dotnet.microsoft.com/download/dotnet/9.0

## 🚀 Getting Started

```bash
# Clone or navigate to the project
cd AutomationContent

# Restore packages
dotnet restore

# Run the app
dotnet run
```

## 🏗️ Build for Production

```bash
# Build release
dotnet publish -c Release -r osx-arm64 --self-contained true
# or for Windows
dotnet publish -c Release -r win-x64 --self-contained true
```

## 🧩 Project Structure

```
AutomationContent/
├── Program.cs                 # Entry point
├── App.cs / App.axaml         # Application class & theme
├── MainWindow.axaml           # Main UI (XAML)
├── MainWindow.axaml.cs        # UI event handlers
├── Models/
│   ├── VideoInfo.cs           # Video metadata model
│   └── VideoQuality.cs        # Quality enum & helpers
├── ViewModels/
│   └── MainViewModel.cs       # Main business logic (MVVM)
└── Services/
    └── YtDlpService.cs        # yt-dlp wrapper service
```

## 📝 How It Works

1. The app wraps **yt-dlp** (a powerful open-source YouTube downloader)
2. Video info is fetched using `yt-dlp --dump-json`
3. Downloads are performed with real-time progress parsing from yt-dlp output
4. FFmpeg handles merging video+audio streams and audio extraction
5. All operations support cancellation via `CancellationToken`

## 🎨 Tech Stack

- **Framework**: .NET 9
- **UI**: Avalonia UI 11.2 (cross-platform)
- **Theme**: Fluent Dark with custom purple accent
- **Pattern**: MVVM (Model-View-ViewModel)
- **Backend**: yt-dlp + FFmpeg (CLI tools)

## 📄 License

MIT License
