# AGENTS.md

## 项目概述

基于 NVEnc 的 Windows 桌面应用，为 NVIDIA RTX 显卡提供视频/图片超分 (VSR)、HDR 转换、视频光流插帧 (FRUC) 功能。支持图片输入自动检测，输出为 PNG。

## 技术栈

- **框架**: .NET 10.0, Avalonia UI 12.0
- **MVVM**: CommunityToolkit.Mvvm (源生成器)
- **架构**: MVVM + ViewLocator (反射映射 ViewModel → View)
- **无 DI 容器**: App.axaml.cs 中手动 `new` 构建依赖链 (ProcessRunner → VideoProbeService → NvencCommandBuilder → EncodeTaskRunner → EncodeQueueService)

## 构建命令

```bash
# 首次构建前先下载内置第三方工具（NVEncC + ffmpeg）到 tools/，CI 会自动执行
./scripts/Download-Tools.ps1
dotnet build RTX-Encode-Toolkit.csproj
dotnet publish RTX-Encode-Toolkit.csproj --configuration Release --runtime win-x64 --self-contained false
```

## 内置第三方工具

- `scripts/Download-Tools.ps1` 下载固定版本（NVEncC 9.30 x64 / ffmpeg n7.1 GPL）并 sha256 校验，平铺 3 个 exe 到 `tools/`（不入 git）
- `tools/*` 由 csproj `CopyToOutputDirectory` 复制到发布产物 `{output}/tools/`；`CheckBundledTools` target 在编译期检查 3 个 exe 存在，缺失即中止
- 工具路径已锁定为内置 `AppContext.BaseDirectory/tools/`，不回退系统 PATH，用户不可配置（`EncodeSettings` 默认值即裸文件名）
- 升级版本：修改脚本顶部 `$NVEncTag` / `$FFmpegAsset` 常量（NVEncC 的 sha256 一并更新，ffmpeg 自动从 checksums.sha256 校验）

## 目录结构

```
Models/        - 数据模型 (EncodeSettings, EncodeTask, EncodePlan, VideoInfo, ...)
ViewModels/    - 视图模型 (MainWindow, EncodeSettingsEditor, EncodeQueue, Settings)
Views/         - Avalonia XAML 视图 + Code-behind
Services/      - 核心业务逻辑
  NvencCommandBuilder  - 命令行构建 (核心，包含图片/视频分支)
  EncodeTaskRunner     - 任务执行器 (预处理→编码→后处理)
  EncodeQueueService   - 任务队列管理 (单任务串行)
  ProcessRunner        - 进程启动与输出捕获
  VideoProbeService    - ffprobe 视频/图片探测
  ToolPathResolver     - 工具路径解析 (PATH/绝对路径)
  Localization         - 硬编码中英文本地化 (LocalizationExtension 用于 AXAML)
Styles/        - 自定义 Avalonia 样式 (WinUI3 风格)
Assets/        - 图标和资源
```

## 关键约束

- **外部工具依赖**: NVEncC64.exe, ffprobe, ffmpeg 必须在 PATH 或设置中配置
- **ViewLocator**: 反射将 `ViewModels.XxxViewModel` → `Views.XxxView`，命名必须严格对应
- **编译绑定**: `AvaloniaUseCompiledBindingsByDefault=true`，AXAML 绑定必须类型安全（`ObservableProperty` 生成的 public 属性）
- **本地化**: 硬编码在 `Localization.cs`，添加新字符串需同时更新 zh-CN 和 en-US
- **任务队列**: 单任务串行
- **没有测试项目**: 代码库无测试，修改后通过 `dotnet build` 验证

## 图片与视频处理差异

| 场景 | 视频 | 图片 |
|------|------|------|
| 输入检测 | 默认 | `NvencCommandBuilder.IsImageFile()` 通过扩展名判断 (.png/.jpg/.bmp/.tiff/.webp) |
| 解码器 | `--avhw` (硬件 CUVID) | `--avsw` (软件 FFmpeg) — NVIDIA 硬件解码不支持 JPEG/MJPEG |
| 输出格式 | `.mkv` (matroska) | `.png` (先编码为临时单帧 MKV，再 ffmpeg 提取) |
| 音频/字幕/章节 | `--audio-copy --sub-copy --chapter-copy ...` | 全部跳过 |
| FRUC 插帧 | 支持 | 自动忽略，日志输出 `[ImageInputSkippedFruc]` |
| `--avsync` | 用户可选 | 不传（无此参数） |
| 文件名标签 | 含编码格式 (`hevc10`, `av1`, `h264`) 和 `hdr10` | 不含编码标签，仅 `_rtx_vsr_{分辨率}_hdr` |

## 图片处理管线

```
输入图片 → ffprobe 探测宽高 → NVEncC --avsw (VSR + HDR, 无 FRUC)
→ 临时单帧 MKV → ffmpeg -vframes 1 提取帧 → 输出 PNG → 删除临时 MKV
```

关键代码路径:
- `NvencCommandBuilder.BuildAsync()` 中 `isImage = IsImageFile(inputPath)` 控制分支
- `EncodePlan.PostprocessCommand` 存储 ffmpeg 帧提取命令，`TemporaryOutputPath` 存储临时 MKV 路径
- `EncodeTaskRunner.RunAsync()` 在 NVEncC 成功后执行 PostprocessCommand
- 临时 MKV 在 `TryDeleteTemporaryInput()` 中清理（`KeepTemporaryFiles` 为 true 时保留）
- 命令预览 `ToCommandPreview()` 顺序: Preprocess → Main → Postprocess

## 编码管线 (视频)

```
输入视频 → ffprobe 探测 → (可选: VFR→CFR ffmpeg 预处理)
→ NVEncC --avhw (VSR / 插帧 / HDR / 反交错) → 输出 MKV
```

- `EncodePlan.PreprocessCommand` 存储 CFR 预处理命令，`TemporaryInputPath` 存储预处理后的文件
- 插帧开启且源为 VFR 时，自动用 ffmpeg (libx264) 预处理为 CFR
- 插帧开启且源帧率已达标时，自动跳过

## 代码风格

- 文件作用域命名空间 (`namespace X;`)
- `record` 用于不可变数据 (`ProcessCommand`, `EncodeTaskProgress`, `ProcessCaptureResult`)
- 异步方法以 `Async` 结尾
- 所有 UI 文字通过 `_localization["Key"]` 访问，禁止硬编码字符串
- 图片扩展名白名单在 `NvencCommandBuilder.ImageExtensions` 静态集合中
