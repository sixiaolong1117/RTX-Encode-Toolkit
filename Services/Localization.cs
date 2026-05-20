using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RTX_Encode_Toolkit.Services;

public sealed class Localization : INotifyPropertyChanged
{
    public static Localization Instance { get; } = new();

    private string _currentCulture = "zh-CN";

    private readonly Dictionary<string, Dictionary<string, string>> _resources = new()
    {
        ["zh-CN"] = new()
        {
            // Window
            ["WindowTitle"] = "RTX Encode Toolkit",

            // Status
            ["StatusReady"] = "准备就绪",
            ["StatusEncoding"] = "准备编码",
            ["StatusCompleted"] = "编码完成",
            ["StatusCancelled"] = "已取消",
            ["StatusFailed"] = "失败",
            ["StatusLogCleared"] = "日志已清空",
            ["StatusInstallingNvenc"] = "正在安装 NVEnc",
            ["StatusNvencInstalled"] = "NVEnc 安装完成",
            ["StatusNvencInstallFailed"] = "NVEnc 安装失败",

            // Buttons
            ["ClearLog"] = "清空日志",
            ["Cancel"] = "取消",
            ["StartEncode"] = "开始编码",
            ["Browse"] = "...",

            // File section
            ["FileSection"] = "文件",
            ["InputVideo"] = "输入视频",
            ["InputPlaceholder"] = "input.mp4",
            ["OutputDirectory"] = "输出目录",
            ["OutputPlaceholder"] = "留空则输出到 rtx_exports",

            // Tool paths
            ["DownloadUpdate"] = "下载/更新",

            // VSR section
            ["VsrSection"] = "RTX VSR 超分",
            ["AutoLongEdge"] = "自动长边",
            ["LongEdgePixels"] = "长边像素",
            ["FixedResolution"] = "固定分辨率",
            ["VsrQuality"] = "VSR 质量",

            // HDR section
            ["HdrSection"] = "RTX HDR / NGX TrueHDR",
            ["Contrast"] = "Contrast",
            ["Saturation"] = "Saturation",
            ["MiddleGray"] = "Middle Gray",
            ["MaxLuminance"] = "Max Luminance",
            ["MaxCLL"] = "MaxCLL",
            ["MasterDisplay"] = "Master Display",

            // Frame Interpolation section
            ["FrucSection"] = "NVOF FRUC 插帧",
            ["TargetFps"] = "目标帧率",
            ["SkipHighFps"] = "高帧率跳过",
            ["CfrPreprocess"] = "CFR 预处理",
            ["CfrFps"] = "CFR 帧率",
            ["PreprocessCrf"] = "预处理 CRF",
            ["PreprocessPreset"] = "预处理预设",

            // Encode section
            ["EncodeSection"] = "编码",
            ["VideoCodec"] = "编码格式",
            ["Qvbr"] = "QVBR",

            ["Preset"] = "Preset",
            ["BFrames"] = "B Frames",
            ["TemporalAq"] = "Temporal AQ",
            ["Deinterlace"] = "反交错",
            ["Avsync"] = "AVSync",
            ["KeepTempFiles"] = "保留 CFR 预处理临时文件",

            // Command preview
            ["CommandPreview"] = "命令预览",

            // Log
            ["Log"] = "日志",
            ["FollowLog"] = "跟随最新日志",

            // NVEnc status
            ["NvencUsingPath"] = "NVEnc：使用 PATH 或手动路径",
            ["NvencNotFound"] = "NVEnc：未发现内置版本，可点击下载/更新",
            ["NvencFound"] = "NVEnc：已发现 ",

            // Log messages
            ["ParsingVideoInfo"] = "解析视频信息并生成命令...",
            ["RunningFfmpegPreprocess"] = "运行 ffmpeg CFR 预处理...",
            ["RunningNvencEncode"] = "运行 NVEncC 编码...",
            ["CompletedOutput"] = "完成：",
            ["TaskCancelled"] = "任务已取消。",
            ["CheckingNvencCapabilities"] = "检查 NVEncC 功能...",
            ["FfmpegPreprocessFailed"] = "ffmpeg 预处理失败，退出码 ",
            ["NvencEncodeFailed"] = "NVEncC 编码失败，退出码 ",
            ["NvencNoVsr"] = "当前 NVEncC64.exe 不包含 RTX VSR / ngx-vsr 支持。",
            ["NvencNoHdr"] = "当前 NVEncC64.exe 不包含 RTX HDR / NGX TrueHDR 支持。",
            ["NvencNoFruc"] = "当前 NVEncC64.exe 不包含 FRUC 插帧支持。",
            ["NvencCheckFeaturesFailed"] = "无法读取 --check-features，继续让 NVEncC 在编码时判断 FRUC 可用性。",
            ["NvencFrucNotConfirmed"] = "未确认 NVOF FRUC=yes；如果 GPU 或驱动不支持，编码阶段会失败。",
            ["NvencDownloadStart"] = "开始下载/更新 NVEncC x64...",
            ["NvencPathUpdated"] = "NVEncC 路径已更新：",

            // File picker
            ["SelectInputVideo"] = "选择输入视频",
            ["VideoFiles"] = "视频文件",
            ["SelectOutputDir"] = "选择输出目录",

            // Command preview notes
            ["FfmpegCfrPrepass"] = "ffmpeg CFR prepass: Auto/Force 模式下可能先运行",

            // Language
            ["Language"] = "语言",
            ["SystemLanguage"] = "跟随系统",
            ["Chinese"] = "简体中文",
            ["English"] = "English",

            // Settings
            ["Settings"] = "设置",
            ["About"] = "关于",
            ["AboutTitle"] = "关于",
            ["SettingsTitle"] = "设置",
            ["ToolPaths"] = "工具路径",
            ["NvencPath"] = "NVEncC 路径",
            ["FfprobePath"] = "ffprobe 路径",
            ["FfmpegPath"] = "ffmpeg 路径",
            ["LanguageSettings"] = "语言设置",
            ["Apply"] = "应用",
            ["Save"] = "保存",
            ["Close"] = "关闭",
        },
        ["en-US"] = new()


        {
            // Window
            ["WindowTitle"] = "RTX Encode Toolkit",

            // Status
            ["StatusReady"] = "Ready",
            ["StatusEncoding"] = "Preparing to encode",
            ["StatusCompleted"] = "Encoding completed",
            ["StatusCancelled"] = "Cancelled",
            ["StatusFailed"] = "Failed",
            ["StatusLogCleared"] = "Log cleared",
            ["StatusInstallingNvenc"] = "Installing NVEnc",
            ["StatusNvencInstalled"] = "NVEnc installed",
            ["StatusNvencInstallFailed"] = "NVEnc installation failed",

            // Buttons
            ["ClearLog"] = "Clear Log",
            ["Cancel"] = "Cancel",
            ["StartEncode"] = "Start Encode",
            ["Browse"] = "...",

            // File section
            ["FileSection"] = "File",
            ["InputVideo"] = "Input Video",
            ["InputPlaceholder"] = "input.mp4",
            ["OutputDirectory"] = "Output Directory",
            ["OutputPlaceholder"] = "Leave empty to output to rtx_exports",

            // Tool paths
            ["DownloadUpdate"] = "Download/Update",

            // VSR section
            ["VsrSection"] = "RTX VSR Upscaling",
            ["AutoLongEdge"] = "Auto Long Edge",
            ["LongEdgePixels"] = "Long Edge Pixels",
            ["FixedResolution"] = "Fixed Resolution",
            ["VsrQuality"] = "VSR Quality",

            // HDR section
            ["HdrSection"] = "RTX HDR / NGX TrueHDR",
            ["Contrast"] = "Contrast",
            ["Saturation"] = "Saturation",
            ["MiddleGray"] = "Middle Gray",
            ["MaxLuminance"] = "Max Luminance",
            ["MaxCLL"] = "MaxCLL",
            ["MasterDisplay"] = "Master Display",

            // Frame Interpolation section
            ["FrucSection"] = "NVOF FRUC Frame Interpolation",
            ["TargetFps"] = "Target FPS",
            ["SkipHighFps"] = "Skip High FPS Source",
            ["CfrPreprocess"] = "CFR Preprocess",
            ["CfrFps"] = "CFR FPS",
            ["PreprocessCrf"] = "Preprocess CRF",
            ["PreprocessPreset"] = "Preprocess Preset",

            // Encode section
            ["EncodeSection"] = "Encode",
            ["VideoCodec"] = "Video Codec",
            ["Qvbr"] = "QVBR",

            ["Preset"] = "Preset",
            ["BFrames"] = "B Frames",
            ["TemporalAq"] = "Temporal AQ",
            ["Deinterlace"] = "Deinterlace",
            ["Avsync"] = "AVSync",
            ["KeepTempFiles"] = "Keep CFR Preprocess Temp Files",

            // Command preview
            ["CommandPreview"] = "Command Preview",

            // Log
            ["Log"] = "Log",
            ["FollowLog"] = "Follow Log",

            // NVEnc status
            ["NvencUsingPath"] = "NVEnc: using PATH or manual path",
            ["NvencNotFound"] = "NVEnc: no built-in version found, click Download/Update",
            ["NvencFound"] = "NVEnc: found ",

            // Log messages
            ["ParsingVideoInfo"] = "Parsing video info and generating commands...",
            ["RunningFfmpegPreprocess"] = "Running ffmpeg CFR preprocess...",
            ["RunningNvencEncode"] = "Running NVEncC encode...",
            ["CompletedOutput"] = "Completed: ",
            ["TaskCancelled"] = "Task cancelled.",
            ["CheckingNvencCapabilities"] = "Checking NVEncC capabilities...",
            ["FfmpegPreprocessFailed"] = "ffmpeg preprocess failed, exit code ",
            ["NvencEncodeFailed"] = "NVEncC encode failed, exit code ",
            ["NvencNoVsr"] = "Current NVEncC64.exe does not support RTX VSR / ngx-vsr.",
            ["NvencNoHdr"] = "Current NVEncC64.exe does not support RTX HDR / NGX TrueHDR.",
            ["NvencNoFruc"] = "Current NVEncC64.exe does not support FRUC frame interpolation.",
            ["NvencCheckFeaturesFailed"] = "Cannot read --check-features, will let NVEncC determine FRUC availability during encoding.",
            ["NvencFrucNotConfirmed"] = "NVOF FRUC=yes not confirmed; encoding may fail if GPU or driver does not support it.",
            ["NvencDownloadStart"] = "Starting download/update NVEncC x64...",
            ["NvencPathUpdated"] = "NVEncC path updated: ",

            // File picker
            ["SelectInputVideo"] = "Select Input Video",
            ["VideoFiles"] = "Video Files",
            ["SelectOutputDir"] = "Select Output Directory",

            // Command preview notes
            ["FfmpegCfrPrepass"] = "ffmpeg CFR prepass: may run first in Auto/Force mode",

            // Language
            ["Language"] = "Language",
            ["SystemLanguage"] = "System",
            ["Chinese"] = "简体中文",
            ["English"] = "English",

            // Settings
            ["Settings"] = "Settings",
            ["About"] = "About",
            ["AboutTitle"] = "About",
            ["SettingsTitle"] = "Settings",
            ["ToolPaths"] = "Tool Paths",
            ["NvencPath"] = "NVEncC Path",
            ["FfprobePath"] = "ffprobe Path",
            ["FfmpegPath"] = "ffmpeg Path",
            ["LanguageSettings"] = "Language Settings",
            ["Apply"] = "Apply",
            ["Save"] = "Save",
            ["Close"] = "Close",
        }


    };

    public string CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture != value)
            {
                _currentCulture = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Strings));

            }
        }
    }

    public string this[string key]
    {
        get
        {
            if (_resources.TryGetValue(_currentCulture, out var cultureResources) &&
                cultureResources.TryGetValue(key, out var value))
            {
                return value;
            }

            // Fallback to zh-CN
            if (_resources.TryGetValue("zh-CN", out var fallbackResources) &&
                fallbackResources.TryGetValue(key, out var fallbackValue))
            {
                return fallbackValue;
            }

            return $"[{key}]";
        }
    }

    public LocalizationStrings Strings => new(this);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class LocalizationStrings
{
    private readonly Localization _localization;

    public LocalizationStrings(Localization localization)
    {
        _localization = localization;
    }

    public string this[string key] => _localization[key];
}




