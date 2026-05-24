# RTX Encode Toolkit

<div align="center">

<img src="Assets/rtx-encode-toolkit.png" alt="RTX Encode Toolkit" width="128">

**基于 [NVEnc](https://github.com/rigaya/NVEnc) 的 NVIDIA RTX 视频编码工具<br/>支持 RTX VSR 超分 · RTX HDR · NVOF FRUC 插帧 · 批量任务队列**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-12.0-8B5CFE)](https://avaloniaui.net/)

[English](README_EN.md) | **简体中文**

</div>

---

## 📖 简介

RTX Encode Toolkit 是一款基于 [NVEnc](https://github.com/rigaya/NVEnc) 的 Windows 桌面应用程序，为 NVIDIA RTX 显卡用户提供图形化界面，轻松调用 RTX 显卡的 AI 视频增强功能。


## 🖼️ 界面预览

![](/Assets/屏幕截图.png)

## ✨ 功能特性

- **RTX VSR (Video Super Resolution)** — AI 视频超分辨率，将低分辨率视频提升至高分辨率
- **RTX HDR (NGX TrueHDR)** — AI 将 SDR 视频转换为 HDR 视频
- **NVOF FRUC (Frame Interpolation)** — AI 光流法插帧，提升视频帧率
- **批量输入** — 支持同时选择多个视频文件，一键添加至任务队列
- **任务队列面板** — 所有任务状态、进度、参数标签一目了然
- **逐任务日志** — 每个任务独立日志视图

## 🚀 快速开始

### 系统要求

- Windows 10/11 64 位
- [.NET 10.0 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- NVIDIA RTX 20/30/40/50 系列显卡
- 最新 NVIDIA 显卡驱动

### 安装

从 [Github Releases](https://github.com/sixiaolong1117/RTX-Encode-Toolkit/releases) 下载最新的 `.zip` 压缩包。解压到任意目录，运行 `RTX-Encode-Toolkit.exe`

### 首次使用

1. 打开软件后，点击标题栏的 **设置** 按钮，填写所需工具的路径
2. 返回主界面，点击 **添加任务**，选择输入视频，调整参数，点击添加
3. 任务将加入队列并自动开始编码

> **提示**：如果 NVEncC / ffprobe / ffmpeg 已存在于系统 PATH 中，软件会自动检测并使用。

## 📖 使用指南

### 基本流程

1. **添加任务** — 点击 **添加任务** 按钮，打开参数配置窗口
2. **选择输入视频** — 点击输入视频旁的 `...` 按钮，可同时选择多个视频文件（批量添加）
3. **设置输出目录** — 可选，留空则输出到每个视频所在目录的 `rtx_exports` 文件夹
4. **选择功能** — 勾选需启用的增强功能（VSR 超分 / HDR / 插帧），可同时启用多项
5. **调整参数** — 根据需求调整各项功能的详细参数
6. **选择编码格式** — 在编码区域选择 HEVC、AV1 或 H264，调整编码参数
7. **确认添加** — 点击 **添加任务**，任务加入队列并自动开始编码
8. **监控队列** — 在主界面队列面板中查看各任务状态和进度

### 任务队列操作

| 操作 | 说明 |
|------|------|
| 查看进度 | 每个任务下方显示进度条、百分比和状态文字 |
| 取消任务 | 点击任务右侧的 **取消** 按钮，正在运行的任务将被终止 |
| 移除任务 | 点击 **移除** 按钮从队列中删除已完成/失败/已取消的任务 |
| 打开文件夹 | 任务完成后点击 **📂** 按钮打开输出文件所在目录 |
| 查看日志 | 在下方日志面板查看选中任务的实时编码日志 |
| 取消全部 | 点击标题栏 **取消所有** 按钮停止所有任务 |
| 自动跟随 | 勾选 **自动跟随日志**，日志视图始终滚动到最新内容 |

### 功能说明

#### RTX VSR 超分

- **自动长边**：根据源视频宽高比自动计算分辨率，只需设置长边像素值
- **固定分辨率**：手动指定输出分辨率，格式如 `3840x-2`（`-2` 表示自动计算短边以保持比例）
- **VSR 质量**：1–4 级，4 为最高质量

#### RTX HDR

- 将 SDR 视频转换为 HDR10 格式
- **对比度**：HDR 映射对比度 (0–200，默认 125)
- **饱和度**：色彩饱和度 (0–200，默认 75)
- **中间灰**：中间灰级别 (0–100，默认 44)
- **最大亮度**：峰值亮度 nits (100–4000，默认 1000)
- **MaxCLL**：最大内容亮度级别，格式 `1000,400`
- **MasterDisplay**：母带显示器色域与亮度信息

#### NVOF FRUC 插帧

- 使用 NVIDIA 光流法进行帧插值，将视频帧率提升至目标帧率
- **目标帧率**：设置期望的输出帧率 (1–240，默认 120)
- **高帧率跳过**：源帧率已达到或超过目标帧率时自动跳过插帧
- **CFR 预处理**：将 VFR（可变帧率）视频转为 CFR（恒定帧率），确保插帧稳定
  - **Auto**：检测到 VFR 时自动预处理
  - **Force**：强制始终预处理
  - **Off**：不预处理
  - **CRF**：预处理质量 (0–51，默认 10)
  - **Preset**：预处理速度预设 (ultrafast–medium，默认 veryfast)

### 编码参数说明

| 参数 | 说明 | 默认值 |
|------|------|--------|
| 编码格式 | HEVC / AV1 / H264 | HEVC |
| QVBR | 质量可变码率，值越小质量越高 (1–51) | 20 |
| Preset | 编码速度预设，P7 最慢质量最高 | P7 |
| B Frames | B 帧数量 (0–8) | 5 |
| Temporal AQ | 时域自适应量化，提升动态场景质量 | 开启 |
| 反交错 | 对隔行扫描视频进行去交错处理 | 关闭 |
| Audio Sync | 音视频同步模式 (auto / forcecfr / vfr) | auto |
| 保留临时文件 | 保留 CFR 预处理等中间文件 | 关闭 |

### 编码格式对比

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

- [rigaya/NVEnc](https://github.com/rigaya/NVEnc) — 强大的 NVIDIA GPU 编码器
- [FFmpeg](https://ffmpeg.org/) — 多媒体处理工具套件
- [Avalonia UI](https://avaloniaui.net/) — 优秀的跨平台 .NET UI 框架
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — .NET MVVM 工具包
