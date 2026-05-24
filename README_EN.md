# RTX Encode Toolkit

<div align="center">

<img src="Assets/rtx-encode-toolkit.png" alt="RTX Encode Toolkit" width="128">

**An NVIDIA RTX video encoding tool based on [NVEnc](https://github.com/rigaya/NVEnc)<br/>Supporting RTX VSR Upscaling · RTX HDR · NVOF FRUC Frame Interpolation · Batch Task Queue**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-12.0-8B5CFE)](https://avaloniaui.net/)

**English** | [简体中文](README.md)

</div>

---

## 📖 Introduction

RTX Encode Toolkit is a Windows desktop application based on [NVEnc](https://github.com/rigaya/NVEnc), providing a graphical interface for NVIDIA RTX GPU users to easily leverage the AI-powered video enhancement capabilities of RTX graphics cards.

## 🖼️ Screenshots

![](/Assets/屏幕截图.png)

## ✨ Features

- **RTX VSR (Video Super Resolution)** — AI video super resolution, upscaling low-resolution videos to higher resolutions
- **RTX HDR (NGX TrueHDR)** — AI-powered SDR to HDR video conversion
- **NVOF FRUC (Frame Interpolation)** — AI optical flow-based frame interpolation for higher frame rates
- **Batch Input** — Select multiple video files at once and add them to the task queue with one click
- **Task Queue Panel** — All task statuses, progress, and parameter labels at a glance
- **Per-Task Logging** — Independent log view for each task

## 🚀 Quick Start

### System Requirements

- Windows 10/11 64-bit
- [.NET 10.0 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- NVIDIA RTX 20/30/40/50 series GPU
- Latest NVIDIA GPU drivers

### Installation

Download the latest `.zip` archive from [GitHub Releases](https://github.com/sixiaolong1117/RTX-Encode-Toolkit/releases). Extract it to any directory and run `RTX-Encode-Toolkit.exe`.

### First Time Use

1. After launching the application, click the **Settings** button in the title bar and configure the paths to the required tools
2. Return to the main window, click **Add Task**, select input videos, adjust parameters, and click Add
3. Tasks will be added to the queue and encoding will start automatically

> **Tip**: If NVEncC / ffprobe / ffmpeg are already in your system PATH, the application will detect and use them automatically.

## 📖 Usage Guide

### Basic Workflow

1. **Add Task** — Click the **Add Task** button to open the parameter configuration window
2. **Select Input Video(s)** — Click the `...` button next to Input Video; you can select multiple video files at once (batch add)
3. **Set Output Directory** — Optional; leave empty to output to the `rtx_exports` folder in each video's directory
4. **Select Features** — Check the enhancement features you need (VSR Upscaling / HDR / Frame Interpolation); multiple features can be enabled simultaneously
5. **Adjust Parameters** — Fine-tune detailed parameters for each feature as needed
6. **Select Codec** — Choose HEVC, AV1, or H264 in the encoding section, and adjust encoding parameters
7. **Confirm Add** — Click **Add Task**; the task joins the queue and encoding starts automatically
8. **Monitor Queue** — View each task's status and progress in the main window's queue panel

### Task Queue Operations

| Action | Description |
|------|------|
| View Progress | Each task displays a progress bar, percentage, and status text below |
| Cancel Task | Click the **Cancel** button to the right of a task; running tasks will be terminated |
| Remove Task | Click the **Remove** button to delete completed/failed/cancelled tasks from the queue |
| Open Folder | After a task completes, click the **📂** button to open the output file's directory |
| View Log | View real-time encoding logs for the selected task in the log panel below |
| Cancel All | Click the **Cancel All** button in the title bar to stop all tasks |
| Auto-Scroll | Check **Auto-Scroll Log** to keep the log view scrolled to the latest content |

### Feature Details

#### RTX VSR Upscaling

- **Auto Long Edge**: Automatically calculates resolution based on source aspect ratio; just set the long edge pixel value
- **Fixed Resolution**: Manually specify output resolution, e.g., `3840x-2` (`-2` means auto-calculate short edge to maintain aspect ratio)
- **VSR Quality**: Levels 1–4, with 4 being the highest quality

#### RTX HDR

- Converts SDR video to HDR10 format
- **Contrast**: HDR mapping contrast (0–200, default 125)
- **Saturation**: Color saturation (0–200, default 75)
- **Middle Gray**: Middle gray level (0–100, default 44)
- **Max Luminance**: Peak brightness in nits (100–4000, default 1000)
- **MaxCLL**: Maximum Content Light Level, format `1000,400`
- **MasterDisplay**: Mastering display color gamut and luminance information

#### NVOF FRUC Frame Interpolation

- Uses NVIDIA Optical Flow for frame interpolation to increase video frame rate to the target FPS
- **Target FPS**: Set the desired output frame rate (1–240, default 120)
- **High FPS Skip**: Automatically skips interpolation when source FPS already meets or exceeds the target
- **CFR Preprocessing**: Converts VFR (Variable Frame Rate) video to CFR (Constant Frame Rate) for stable interpolation
  - **Auto**: Automatically preprocess when VFR is detected
  - **Force**: Always preprocess
  - **Off**: No preprocessing
  - **CRF**: Preprocessing quality (0–51, default 10)
  - **Preset**: Preprocessing speed preset (ultrafast–medium, default veryfast)

### Encoding Parameters

| Parameter | Description | Default |
|------|------|--------|
| Codec | HEVC / AV1 / H264 | HEVC |
| QVBR | Quality Variable Bitrate; lower value = higher quality (1–51) | 20 |
| Preset | Encoding speed preset; P7 = slowest, highest quality | P7 |
| B Frames | Number of B frames (0–8) | 5 |
| Temporal AQ | Temporal Adaptive Quantization for improved dynamic scene quality | On |
| Deinterlace | Deinterlacing for interlaced source video | Off |
| Audio Sync | Audio-video sync mode (auto / forcecfr / vfr) | auto |
| Keep Temp Files | Keep intermediate files such as CFR preprocessing output | Off |

### Codec Comparison

| Codec | Bit Depth | Use Case |
|---------|------|---------|
| HEVC (h265) | 10bit | Default recommendation, good compatibility, excellent quality |
| AV1 | 10bit | Latest encoding standard, highest compression ratio; requires RTX 40 series+ |
| H264 | 8bit | Best compatibility, suitable for playback on older devices |

## 🤝 Contributing

Issues and Pull Requests are welcome!

## 📄 License

This project is open source under the [MIT License](LICENSE).

## 🙏 Acknowledgements

- [rigaya/NVEnc](https://github.com/rigaya/NVEnc) — Powerful NVIDIA GPU encoder
- [FFmpeg](https://ffmpeg.org/) — Multimedia processing tool suite
- [Avalonia UI](https://avaloniaui.net/) — Excellent cross-platform .NET UI framework
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — .NET MVVM toolkit