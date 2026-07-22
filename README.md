# 🎥🍉 VideoHarvester

A modern WPF desktop application for downloading videos from multiple sources including Wistia, YouTube, and more.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Platform](https://img.shields.io/badge/platform-Windows-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## ✨ Features

- **Multi-Source Support**: Download videos from:
  - Wistia
  - YouTube (video)
  - YouTube (audio/WAV format)

- **Queue Management**: Add multiple videos to download queue
- **Download History**: Track all downloaded videos with SQLite database
- **Dependency Checker**: Automatic verification of required tools (Node.js, Python, FFmpeg, yt-dlp)
- **Modern UI**: Clean, intuitive WPF interface with MVVM architecture
- **Batch Processing**: Queue multiple downloads and process them sequentially
- **Metadata Extraction**: Automatically retrieves video title, quality, thumbnail, and file size
- **Real-time Progress**: Monitor download progress with status updates

## 📋 Prerequisites

Before running VideoHarvester, ensure you have the following dependencies installed:

- **Windows** operating system
- **.NET 10.0** runtime or SDK
- **Node.js** (for JavaScript runtime requirements)
- **Python** (for yt-dlp)
- **FFmpeg** (for video processing)
- **yt-dlp** (Python package: `pip install yt-dlp`)

The application will check for these dependencies on startup and display their status.

## 🚀 Installation

1. Clone the repository:
```bash
git clone https://github.com/pk-dev-cs/VideoHarvester.git
cd VideoHarvester
```

2. Build the project:
```bash
dotnet build
```

3. Run the application:
```bash
dotnet run --project VideoHarvester
```

## 🎮 Usage

1. **Select Video Source**: Choose between Wistia, YouTube, or YouTube WAV from the dropdown
2. **Enter Video ID**: Paste the video ID or URL
3. **Add to Queue**: Click "Add to Queue" to add the video to download list
4. **Start Download**: Click "Download All" to begin downloading queued videos
5. **View History**: Switch to the History tab to see previously downloaded videos

### Video ID Examples

- **Wistia**: `abc123xyz` (from URL: `https://example.wistia.com/medias/abc123xyz`)
- **YouTube**: `dQw4w9WgXcQ` (from URL: `https://www.youtube.com/watch?v=dQw4w9WgXcQ`)



## 📦 Dependencies

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (8.4.0) - MVVM helpers
- [HtmlAgilityPack](https://html-agility-pack.net/) (1.12.0) - HTML parsing
- [Microsoft.Data.Sqlite](https://docs.microsoft.com/ef/core/providers/sqlite/) (9.0.1) - Database
- [Microsoft.Extensions.DependencyInjection](https://docs.microsoft.com/dotnet/core/extensions/dependency-injection) (9.0.3) - DI container



## 🔧 Configuration

Downloaded videos are saved to the "Downloads" folder in the application directory by default. Download history is stored in a SQLite database (`download_history.db`).

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

- YouTube downloads require Firefox browser for cookie extraction
- Some videos may require specific quality format adjustments

## 📝 Changelog

### Version 1.0.0
- Initial release
- Support for Wistia and YouTube downloads
- Queue management system
- Download history tracking
- Dependency checking

---

Made with 🩵 using .NET 10 and WPF
