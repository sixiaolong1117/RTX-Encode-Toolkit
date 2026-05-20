# RTX Encode Toolkit

<div align="center">

<img src="Assets/rtx-encode-toolkit.png" alt="RTX Encode Toolkit" width="256">

**基于 [NVEnc](https://github.com/rigaya/NVEnc) 的 NVIDIA RTX 视频编码工具，支持 RTX VSR 超分、RTX HDR、NVOF FRUC 插帧**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](RTX-Encode-Toolkit.csproj)
[![Avalonia](https://img.shields.io/badge/Avalonia-UI-8B5CFE)](https://avaloniaui.net/)

[English](README_EN.md) | **简体中文**

</div>

---

## 📖 简介

RTX Encode Toolkit 是一款基于 [NVEnc](https://github.com/rigaya/NVEnc) 的 Windows 桌面应用程序，为 NVIDIA RTX 显卡用户提供图形化界面，轻松调用 RTX 显卡的 AI 视频增强功能：

- **RTX VSR (Video Super Resolution)** — AI 视频超分辨率，将低分辨率视频提升至高分辨率
- **RTX HDR (NGX TrueHDR)** — AI 将 SDR 视频转换为 HDR 视频
- **NVOF FRUC (Frame Interpolation)** — AI 光流法插帧，提升视频帧率

## ✨ 功能特性

- 🖥️ **图形化界面** — 基于 Avalonia UI 框架，界面简洁直观
- 🌐 **国际化** — 支持简体中文和英文界面
- 🎯 **RTX VSR 超分** — 支持自动长边和固定分辨率两种模式，质量 1-4 级可调
- 🌈 **RTX HDR** — 支持对比度、饱和度、中间灰、最大亮度等参数调节
- ⚡ **NVOF FRUC 插帧** — 支持目标帧率设置、高帧率源自动跳过、CFR 预处理
- 🎞️ **多种编码格式** — 支持 HEVC (h265)、AV1、H264 三种编码格式
- ⚙️ **编码参数调节** — QVBR、Preset、B Frames、Temporal AQ、反交错等
- 🔧 **工具路径管理** — 支持 NVEncC、ffprobe、ffmpeg 路径配置，自动检测 PATH
- 📥 **一键下载** — 内置 NVEncC 和 FFmpeg 下载链接，方便获取最新版本
- 📋 **命令预览** — 实时显示即将执行的 NVEncC 命令
- 📝 **日志输出** — 实时显示编码进度和日志

## 🖼️ 界面预览

![](/Assets/屏幕截图.png)

## 🚀 快速开始

### 系统要求

- Windows 10/11 64 位
- [.NET 10.0 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- NVIDIA RTX 20/30/40/50 系列显卡
- 最新 NVIDIA 显卡驱动

### 下载安装

1. 从 [Releases](https://github.com/sixiaolong1117/RTX-Encode-Toolkit/releases) 页面下载最新版本
2. 解压到任意目录
3. 运行 `RTX-Encode-Toolkit.exe`

### 首次使用

1. 打开软件后，点击 **设置** 按钮
2. 在设置页面中，点击 NVEncC 旁的 **下载/更新** 按钮打开 NVEncC 的 GitHub Release 页面
3. 下载 `NVEncC_x64.7z` 并解压到软件目录下的 `tools/nvenc/` 文件夹
4. 同样下载 FFmpeg（ffmpeg/ffprobe）并放置到 `tools/ffmpeg/` 文件夹
5. 返回主界面，选择输入视频，调整参数，点击 **开始编码**

> **提示**：如果 NVEncC/ffprobe/ffmpeg 已存在于系统 PATH 中，软件会自动检测并使用。

## 📖 使用指南

### 基本流程

1. **选择输入视频** — 点击输入视频旁的 `...` 按钮选择视频文件
2. **设置输出目录** — 可选，留空则输出到视频所在目录的 `rtx_exports` 文件夹
3. **选择功能** — 勾选需要启用的功能（VSR 超分 / HDR / 插帧），至少启用一项
4. **调整参数** — 根据需求调整各功能的详细参数
5. **选择编码格式** — 在编码区域选择 HEVC、AV1 或 H264
6. **开始编码** — 点击 **开始编码** 按钮，等待编码完成

### 功能说明

#### RTX VSR 超分
- **自动长边**：根据源视频比例自动计算分辨率，只需设置长边像素
- **固定分辨率**：手动指定输出分辨率，格式如 `3840x-2`（-2 表示自动计算短边）
- **VSR 质量**：1-4 级，4 为最高质量

#### RTX HDR
- 将 SDR 视频转换为 HDR10 格式
- 支持调节对比度、饱和度、中间灰、最大亮度等 HDR 参数

#### NVOF FRUC 插帧
- 使用 NVIDIA 光流法进行帧插值，提升视频帧率
- **CFR 预处理**：对于 VFR（可变帧率）视频，先用 ffmpeg 转换为 CFR（恒定帧率）
- **高帧率跳过**：源帧率已达到目标帧率时自动跳过插帧

### 编码格式说明

| 编码格式 | 色深 | 适用场景 |
|---------|------|---------|
| HEVC (h265) | 10bit | 默认推荐，兼容性好，画质优秀 |
| AV1 | 10bit | 最新编码标准，压缩率最高，需 RTX 40 系列以上 |
| H264 | 8bit | 兼容性最好，适合老旧设备播放 |

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📄 许可证

本项目基于 [MIT 许可证](LICENSE) 开源。

## 🙏 致谢

- [rigaya/NVEnc](https://github.com/rigaya/NVEnc) — 强大的 NVIDIA 编码器
- [FFmpeg](https://ffmpeg.org/) — 多媒体处理工具
- [Avalonia UI](https://avaloniaui.net/) — 优秀的跨平台 UI 框架
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM 工具包
