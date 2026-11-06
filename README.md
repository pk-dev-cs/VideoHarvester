# 🎥🍉 VideoHarvester

A modern WPF desktop application for downloading videos from Wistia, YouTube, and CDA.pl.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Platform](https://img.shields.io/badge/platform-Windows-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## ✨ Features

- **Multi-Source Support**: Download videos from:
  - Wistia
  - YouTube (video)
  - YouTube (audio/WAV format)
  - CDA.pl

- **Queue Management**: Add videos to the queue and start, pause, stop, or remove individual items
- **Download History**: Track downloads with SQLite, remove individual entries, or clear all history without deleting downloaded files
- **Dependency Checker**: Automatic verification of required tools (Node.js, Python, FFmpeg, yt-dlp)
- **Modern UI**: Clean, intuitive WPF interface with MVVM architecture
- **Batch Processing**: Run 1–5 downloads concurrently
- **Download Preferences**: Configure the download folder, auto-start, preferred video quality, and optional file numbering
- **Metadata Extraction**: Automatically retrieves video title, quality, thumbnail, and file size
- **Real-time Progress**: Monitor download progress with status updates

## 📋 Prerequisites

Before running VideoHarvester, ensure you have the following dependencies installed:

- **Windows** operating system
- **.NET 10.0 Desktop Runtime** to run the app, or the **.NET 10.0 SDK** to build it
- **Node.js** (for JavaScript runtime requirements)
- **Python** (for yt-dlp)
- **FFmpeg** (for video processing)
- **yt-dlp** (Python package: `python -m pip install --upgrade yt-dlp`)

The application will check for Node.js, Python, FFmpeg, and yt-dlp on startup and display their status. Python and FFmpeg must be available in `PATH`; Node.js is detected in `PATH` or its standard installation folders. Python and yt-dlp are used for YouTube and CDA; Node.js is used for YouTube, and FFmpeg is used for media processing and video thumbnails.

## 🚀 Installation

To use a published build, download the ZIP from [GitHub Releases](https://github.com/pk-dev-cs/VideoHarvester/releases), extract all files to a writable folder, and run `VideoHarvester.exe`. The runtime and external tools listed above are installed separately. Older releases may not include all features described here.

To build from source:

1. Clone the repository:
```bash
git clone https://github.com/pk-dev-cs/VideoHarvester.git
cd VideoHarvester
```

2. Build the project:
```bash
dotnet build VideoHarvester.sln
```

3. Run the application:
```bash
dotnet run --project VideoHarvester
```

## 🎮 Usage

1. **Select Video Source**: Choose Wistia, YouTube, YouTubeWav, or Cda from the dropdown
2. **Enter Video Input**: Paste a YouTube URL, a CDA URL or ID, or Wistia HTML as shown below
3. **Add to Queue**: Click "Add to queue" to add the video to the download list; enable "Order numeration" beforehand to number the output file
4. **Start Download**: Downloads start automatically by default. If auto-start is disabled in Settings, use the item's play button. Pause, stop, and remove buttons control individual items
5. **View History**: Open "Download History" from the menu. Use "Refresh", remove individual entries, or select "Clear all" and confirm

For YouTube downloads, the app reads cookies from the default Windows browser for HTTPS links when it supports cookie extraction. If no supported browser is detected, it attempts the download without browser cookies. Reading cookies may fail if the browser profile cannot be accessed or decrypted.

### Video ID Examples

- **Wistia**: `<a href="https://example.com/watch?wvideo=abc123xyz">Watch video</a>` — the current parser requires HTML containing a link with `wvideo=`, rather than a bare ID or plain URL
- **YouTube / YouTubeWav**: `https://www.youtube.com/watch?v=dQw4w9WgXcQ` — use a full URL, not a bare video ID
- **Cda**: `https://www.cda.pl/video/3383563ba` or `3383563ba`

Output formats are MKV for YouTube video, WAV for YouTubeWav, and MP4 for Wistia and CDA.



## 📦 Dependencies

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (8.4.2) - MVVM helpers
- [HtmlAgilityPack](https://html-agility-pack.net/) (1.12.4) - HTML parsing
- [Microsoft.Data.Sqlite](https://docs.microsoft.com/ef/core/providers/sqlite/) (10.0.10) - Database
- [SQLitePCLRaw.bundle_e_sqlite3](https://github.com/ericsink/SQLitePCL.raw) (2.1.12) - Native SQLite bundle
- [Microsoft.Extensions.DependencyInjection](https://docs.microsoft.com/dotnet/core/extensions/dependency-injection) (10.0.10) - DI container



## 🔧 Configuration

Downloaded files are saved to the `VideoHarvester` folder inside the current user's Windows Videos folder by default. Change the location in **Settings** and click **Save Settings**. Settings are stored in `%APPDATA%\VideoHarvester\settings.json`; download history is stored in `downloads.db` beside the application executable, so that folder must be writable.

Settings also include auto-start (enabled by default), maximum concurrent downloads (1–5), and preferred quality (`Best`, `1080p`, `720p`, `480p`, or `360p`). The folder and quality are captured when an item is added to the queue. The "Show notifications" option is saved, but completion notifications are not yet implemented.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the project
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 👤 Author

**pk-dev-cs**
- GitHub: [@pk-dev-cs](https://github.com/pk-dev-cs)

## ⚠️ Disclaimer

This tool is for personal use only. Please respect copyright laws and terms of service of video platforms. Only download videos you have the right to download.

## 🐛 Known Issues

- Some videos may require specific quality format adjustments
- Wistia downloads retain `.part` and `.part.json` files while paused. Starting the same queue item resumes from the saved position when the server supports safe continuation; otherwise it reports an error and preserves the partial file. Only a complete download becomes an MP4 file
- Pausing YouTube or CDA cancels the current operation; restarting it relies on yt-dlp's resume support
- Queued items are not restored after restarting the application; download history is preserved

---

Made with 🩵 using .NET 10 and WPF
