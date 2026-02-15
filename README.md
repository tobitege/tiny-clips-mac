> [!IMPORTANT]
> **Windows Port Branch Notice:** this branch contains the Windows 11 / C# / .NET 9 migration of TinyClips.  
> The original project README content is preserved below for continuity, with Windows-specific updates layered on top.

# TinyClips for macOS

[![Build](https://github.com/jamesmontemagno/tiny-clips-mac/actions/workflows/build.yml/badge.svg)](https://github.com/jamesmontemagno/tiny-clips-mac/actions/workflows/build.yml)
[![Release](https://github.com/jamesmontemagno/tiny-clips-mac/actions/workflows/release.yml/badge.svg)](https://github.com/jamesmontemagno/tiny-clips-mac/actions/workflows/release.yml)
[![GitHub release](https://img.shields.io/github/v/release/jamesmontemagno/tiny-clips-mac?style=flat-square)](https://github.com/jamesmontemagno/tiny-clips-mac/releases/latest)
![macOS](https://img.shields.io/badge/macOS-15.0+-blue?style=flat-square&logo=apple)
![Swift](https://img.shields.io/badge/Swift-5.9-orange?style=flat-square&logo=swift)
[![License: MIT](https://img.shields.io/github/license/jamesmontemagno/tiny-clips-mac?style=flat-square)](LICENSE)

> **Windows Status Update:** This branch now targets **Windows 11 (.NET 9 WPF)** with screenshot, MP4, and GIF region capture.

TinyClips is a lightweight app for capturing screenshots (PNG), video (MP4), and animated GIFs of a selected screen region.

![](./docs/tinyclips.png)

## Features

- **Screenshot** - Capture a selected region to PNG
- **Video Recording** - Record a selected region to MP4
- **GIF Recording** - Record a selected region as an animated GIF
- **Countdowns** - Optional pre-record countdown for video and GIF
- **Region Selection** - Fullscreen drag-to-select overlay across virtual desktop
- **Settings Persistence** - Settings stored at `%LocalAppData%\TinyClips.Windows\settings.json`
- **Clipboard Support** - Optional screenshot copy to clipboard
- **Explorer Reveal** - Optional reveal output file in Explorer after save
- **Win32 Interop** - Uses Vanara (`User32`, `Gdi32`) for native window behavior

## Requirements

- Windows 11
- .NET 9 SDK (or Visual Studio 2022 17.12+)

## Installation / Build

### Build from source

```powershell
dotnet restore TinyClips.Windows.sln
dotnet build TinyClips.Windows.sln -c Release
```

### Run

```powershell
dotnet run --project src/TinyClips.Windows/TinyClips.Windows.csproj
```

## Usage

1. Launch the app.
2. Choose one of the capture actions:
   - **Screenshot**
   - **Record Video**
   - **Record GIF**
3. Drag to select the screen region.
4. For video/GIF, stop recording with **Stop Active Recording**.
5. Find outputs in the configured save folder.

## Keyboard / Controls

| Action | Control |
|--------|---------|
| Select capture area | Click + drag |
| Cancel region selection | `Esc` |
| Finish recording | Click **Stop Active Recording** |

## Settings

| Option | Description |
|--------|-------------|
| Save Directory | Output folder for screenshots and recordings |
| Copy screenshot to clipboard | Copies screenshot captures to clipboard |
| Reveal output in Explorer | Opens Explorer and selects the saved file |
| Video FPS | Recording frame rate for MP4 (`24`, `30`, `60`) |
| Video countdown | Enable/disable countdown before video starts |
| Video countdown duration | Countdown duration in seconds |
| GIF FPS | Animated GIF frame rate (`5-30`) |
| GIF Max Width | Max output width for GIF (`320-1920`) |
| GIF countdown | Enable/disable countdown before GIF recording |
| GIF countdown duration | Countdown duration in seconds |

## Project layout

- `TinyClips.Windows.sln` - Visual Studio solution
- `src/TinyClips.Windows/` - Windows app source code
  - `MainWindow.*` - Main UI and capture workflows
  - `Views/RegionSelectionWindow.*` - Region selector overlay
  - `Services/ScreenCaptureService.cs` - Frame capture and image save
  - `Services/RecordingSession.cs` - MP4/GIF recording pipeline
  - `Services/CountdownService.cs` - Countdown workflow
  - `Models/CaptureSettings.cs` - Persistent settings model

## Notes

- Legacy macOS Swift/Xcode sources (`TinyClips/`, `TinyClips.xcodeproj`) are kept in the repository for migration reference.
- This Linux container environment cannot execute Windows WPF UI directly.

## License

MIT. See `LICENSE`.
