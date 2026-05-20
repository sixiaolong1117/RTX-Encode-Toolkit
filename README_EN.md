# RTX Encode Toolkit

<div align="center">

<img src="Assets/rtx-encode-toolkit.png" alt="RTX Encode Toolkit" width="256">

**An NVIDIA RTX video encoding tool based on [NVEnc](https://github.com/rigaya/NVEnc), supporting RTX VSR upscaling, RTX HDR, and NVOF FRUC frame interpolation**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](RTX-Encode-Toolkit.csproj)
[![Avalonia](https://img.shields.io/badge/Avalonia-UI-8B5CFE)](https://avaloniaui.net/)

**English** | [简体中文](README.md)

</div>

---

## 📖 Introduction

RTX Encode Toolkit is a Windows desktop application based on [NVEnc](https://github.com/rigaya/NVEnc), providing a graphical interface for NVIDIA RTX GPU users to easily leverage AI-powered video enhancement features:

- **RTX VSR (Video Super Resolution)** — AI upscaling to enhance low-resolution videos to higher resolutions
- **RTX HDR (NGX TrueHDR)** — AI-powered SDR to HDR conversion
- **NVOF FRUC (Frame Interpolation)** — AI optical flow-based frame interpolation for smoother video

## ✨ Features

- 🖥️ **Graphical Interface** — Built with Avalonia UI framework, clean and intuitive
- 🌐 **Internationalization** — Supports Simplified Chinese and English
- 🎯 **RTX VSR Upscaling** — Auto long edge and fixed resolution modes, quality levels 1-4
- 🌈 **RTX HDR** — Adjustable contrast, saturation, middle gray, max luminance, and more
- ⚡ **NVOF FRUC** — Target FPS setting, auto-skip for high FPS sources, CFR preprocessing
- 🎞️ **Multiple Codecs** — Supports HEVC (h265), AV1, and H264
- ⚙️ **Encoding Parameters** — QVBR, Preset, B Frames, Temporal AQ, Deinterlace, etc.
- 🔧 **Tool Path Management** — Configure NVEncC, ffprobe, ffmpeg paths with auto PATH detection
- 📥 **One-Click Download** — Built-in links to download NVEncC and FFmpeg
- 📋 **Command Preview** — Real-time preview of the NVEncC command to be executed
- 📝 **Log Output** — Real-time encoding progress and logs

## 🖼️ Screenshots

![](/Assets/屏幕截图.png)

## 🚀 Quick Start

### System Requirements

- Windows 10/11 64-bit
- [.NET 10.0 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- NVIDIA RTX 20/30/40/50 series GPU
- Latest NVIDIA GPU drivers

### Installation

1. Download the latest release from the [Releases](https://github.com/sixiaolong1117/RTX-Encode-Toolkit/releases) page
2. Extract to any directory
3. Run `RTX-Encode-Toolkit.exe`

### First Time Use

1. Open the application and click the **Settings** button
2. In the settings page, click the **Download/Update** button next to NVEncC to open the NVEncC GitHub Release page
3. Download `NVEncC_x64.7z` and extract it to the `tools/nvenc/` folder in the application directory
4. Similarly, download FFmpeg (ffmpeg/ffprobe) and place them in the `tools/ffmpeg/` folder
5. Return to the main window, select an input video, adjust parameters, and click **Start Encode**

> **Tip**: If NVEncC/ffprobe/ffmpeg are already in your system PATH, the application will detect and use them automatically.

## 📖 Usage Guide

### Basic Workflow

1. **Select Input Video** — Click the `...` button next to the input video field
2. **Set Output Directory** — Optional; leave empty to output to the `rtx_exports` folder in the video's directory
3. **Select Features** — Check the features you need (VSR / HDR / Frame Interpolation), at least one required
4. **Adjust Parameters** — Fine-tune parameters for each feature
5. **Select Codec** — Choose HEVC, AV1, or H264 in the encoding section
6. **Start Encoding** — Click **Start Encode** and wait for completion

### Feature Details

#### RTX VSR Upscaling
- **Auto Long Edge**: Automatically calculates resolution based on source aspect ratio; just set the long edge
- **Fixed Resolution**: Manually specify output resolution, e.g., `3840x-2` (-2 means auto-calculate short edge)
- **VSR Quality**: Levels 1-4, with 4 being the highest quality

#### RTX HDR
- Converts SDR video to HDR10 format
- Adjustable contrast, saturation, middle gray, max luminance, and other HDR parameters

#### NVOF FRUC Frame Interpolation
- Uses NVIDIA optical flow for frame interpolation to increase video frame rate
- **CFR Preprocessing**: For VFR (Variable Frame Rate) videos, uses ffmpeg to convert to CFR (Constant Frame Rate) first
- **High FPS Skip**: Automatically skips interpolation when source FPS already meets the target

### Codec Comparison

| Codec | Bit Depth | Use Case |
|-------|-----------|----------|
| HEVC (h265) | 10bit | Default recommendation, good compatibility, excellent quality |
| AV1 | 10bit | Latest standard, highest compression, requires RTX 40 series+ |
| H264 | 8bit | Best compatibility, suitable for older devices |

## 🤝 Contributing

Issues and Pull Requests are welcome!

## 📄 License

This project is open source under the [MIT License](LICENSE).

## 🙏 Acknowledgements

- [rigaya/NVEnc](https://github.com/rigaya/NVEnc) — Powerful NVIDIA encoder
- [FFmpeg](https://ffmpeg.org/) — Multimedia processing tools
- [Avalonia UI](https://avaloniaui.net/) — Excellent cross-platform UI framework
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM toolkit
