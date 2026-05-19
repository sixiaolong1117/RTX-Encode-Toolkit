using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RTX_Encode_Toolkit.Models;
using RTX_Encode_Toolkit.Services;

namespace RTX_Encode_Toolkit.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ProcessRunner _processRunner = new();
    private readonly NvencCommandBuilder _commandBuilder;
    private readonly NvencToolManager _nvencToolManager = new();
    private CancellationTokenSource? _encodeCancellation;

    [ObservableProperty]
    private string _nvencPath = "NVEncC64.exe";

    [ObservableProperty]
    private string _ffprobePath = "ffprobe.exe";

    [ObservableProperty]
    private string _ffmpegPath = "ffmpeg.exe";

    [ObservableProperty]
    private string _inputPath = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private bool _enableVsr = true;

    [ObservableProperty]
    private bool _autoVsrResolution = true;

    [ObservableProperty]
    private int _vsrLongEdge = 3840;

    [ObservableProperty]
    private string _vsrResolution = "3840x-2";

    [ObservableProperty]
    private int _vsrQuality = 4;

    [ObservableProperty]
    private bool _enableHdr;

    [ObservableProperty]
    private int _hdrContrast = 125;

    [ObservableProperty]
    private int _hdrSaturation = 75;

    [ObservableProperty]
    private int _hdrMiddleGray = 44;

    [ObservableProperty]
    private int _hdrMaxLuminance = 1000;

    [ObservableProperty]
    private string _hdrMaxCll = "1000,400";

    [ObservableProperty]
    private string _hdrMasterDisplay = "G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,1)";

    [ObservableProperty]
    private bool _enableFrameInterpolation;

    [ObservableProperty]
    private int _targetFps = 120;

    [ObservableProperty]
    private bool _skipFrucWhenSourceFpsIsHigh = true;

    [ObservableProperty]
    private string _frucNormalizeMode = "Auto";

    [ObservableProperty]
    private double _frucNormalizeFps;

    [ObservableProperty]
    private int _frucNormalizeCrf = 10;

    [ObservableProperty]
    private string _frucNormalizePreset = "veryfast";

    [ObservableProperty]
    private bool _keepTemporaryFiles;

    [ObservableProperty]
    private bool _deinterlace;

    [ObservableProperty]
    private int _qvbr = 20;

    [ObservableProperty]
    private string _nvencPreset = "P7";

    [ObservableProperty]
    private int _bFrames = 5;

    [ObservableProperty]
    private bool _temporalAq = true;

    [ObservableProperty]
    private string _avsync = "auto";

    [ObservableProperty]
    private string _statusText = "准备就绪";

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private string _lastOutputPath = string.Empty;

    [ObservableProperty]
    private bool _isEncoding;

    [ObservableProperty]
    private bool _isToolInstalling;

    [ObservableProperty]
    private string _nvencToolStatus = "NVEnc：使用 PATH 或手动路径";

    public IReadOnlyList<string> NvencPresets { get; } = ["P7", "P6", "P5", "P4", "P3", "P2", "P1"];

    public IReadOnlyList<string> AvsyncModes { get; } = ["auto", "forcecfr", "vfr"];

    public IReadOnlyList<string> NormalizeModes { get; } = ["Auto", "Force", "Off"];

    public IReadOnlyList<string> NormalizePresets { get; } =
        ["ultrafast", "superfast", "veryfast", "faster", "fast", "medium"];

    public bool IsBusy => IsEncoding || IsToolInstalling;

    public string CommandPreview
    {
        get
        {
            var settings = CreateSettings();
            var builder = new StringBuilder();
            if (settings.EnableFrameInterpolation && settings.FrucNormalizeMode != "Off")
            {
                builder.AppendLine("ffmpeg CFR prepass: Auto/Force 模式下可能先运行");
                builder.AppendLine();
            }

            builder.Append(_commandBuilder.BuildPreviewCommand(settings).ToCommandLine());
            return builder.ToString();
        }
    }

    public MainWindowViewModel()
    {
        var videoProbeService = new VideoProbeService(_processRunner);
        _commandBuilder = new NvencCommandBuilder(videoProbeService);
        UseExistingManagedNvenc();

        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(CommandPreview) or nameof(LogText) or nameof(IsBusy))
            {
                return;
            }

            OnPropertyChanged(nameof(CommandPreview));
            if (args.PropertyName is nameof(IsEncoding) or nameof(IsToolInstalling))
            {
                OnPropertyChanged(nameof(IsBusy));
            }

            StartEncodeCommand.NotifyCanExecuteChanged();
            CancelEncodeCommand.NotifyCanExecuteChanged();
            InstallOrUpdateNvencCommand.NotifyCanExecuteChanged();
        };
    }

    public void SetInputPath(string path)
    {
        InputPath = path;
    }

    public void SetOutputDirectory(string path)
    {
        OutputDirectory = path;
    }

    [RelayCommand(CanExecute = nameof(CanStartEncode))]
    private async Task StartEncodeAsync()
    {
        EncodeSettings? settings = null;
        EncodePlan? plan = null;
        _encodeCancellation = new CancellationTokenSource();
        IsEncoding = true;
        LogText = string.Empty;
        StatusText = "准备编码";

        var progress = new Progress<string>(AppendProcessLine);

        try
        {
            settings = CreateSettings();
            AppendStatus("解析视频信息并生成命令...");
            plan = await _commandBuilder.BuildAsync(settings, _encodeCancellation.Token);
            LastOutputPath = plan.OutputPath;

            foreach (var note in plan.Notes)
            {
                AppendStatus(note);
            }

            var outputDirectory = Path.GetDirectoryName(plan.OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await EnsureNvencCapabilitiesAsync(settings, _encodeCancellation.Token);

            if (plan.PreprocessCommand is not null)
            {
                AppendStatus("运行 ffmpeg CFR 预处理...");
                AppendCommand(plan.PreprocessCommand);
                var prepassExitCode = await _processRunner.RunAsync(plan.PreprocessCommand, progress, _encodeCancellation.Token);
                if (prepassExitCode != 0)
                {
                    throw new InvalidOperationException($"ffmpeg 预处理失败，退出码 {prepassExitCode}。");
                }
            }

            AppendStatus("运行 NVEncC 编码...");
            AppendCommand(plan.MainCommand);
            var exitCode = await _processRunner.RunAsync(plan.MainCommand, progress, _encodeCancellation.Token);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"NVEncC 编码失败，退出码 {exitCode}。");
            }

            StatusText = "编码完成";
            AppendStatus("完成：" + plan.OutputPath);
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消";
            AppendStatus("任务已取消。");
        }
        catch (Exception ex)
        {
            StatusText = "失败";
            AppendStatus(ex.Message);
        }
        finally
        {
            TryDeleteTemporaryInput(plan, settings);
            _encodeCancellation.Dispose();
            _encodeCancellation = null;
            IsEncoding = false;
        }
    }

    private bool CanStartEncode()
    {
        return !IsEncoding && !IsToolInstalling && !string.IsNullOrWhiteSpace(InputPath);
    }

    [RelayCommand(CanExecute = nameof(CanCancelEncode))]
    private void CancelEncode()
    {
        _encodeCancellation?.Cancel();
    }

    private bool CanCancelEncode()
    {
        return IsEncoding;
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogText = string.Empty;
        StatusText = "日志已清空";
    }

    [RelayCommand(CanExecute = nameof(CanInstallOrUpdateNvenc))]
    private async Task InstallOrUpdateNvencAsync()
    {
        IsToolInstalling = true;
        StatusText = "正在安装 NVEnc";
        var progress = new Progress<string>(message => AppendStatus("[NVEnc] " + message));

        try
        {
            AppendStatus("开始下载/更新 NVEncC x64...");
            var result = await _nvencToolManager.InstallOrUpdateAsync(progress, CancellationToken.None);
            NvencPath = result.ExecutablePath;
            NvencToolStatus = $"NVEnc：{result.VersionTag} ({result.InstallDirectory})";
            StatusText = "NVEnc 安装完成";
            AppendStatus("NVEncC 路径已更新：" + result.ExecutablePath);
        }
        catch (Exception ex)
        {
            StatusText = "NVEnc 安装失败";
            AppendStatus("NVEnc 安装失败：" + ex.Message);
        }
        finally
        {
            IsToolInstalling = false;
        }
    }

    private bool CanInstallOrUpdateNvenc()
    {
        return !IsEncoding && !IsToolInstalling;
    }

    private EncodeSettings CreateSettings()
    {
        return new EncodeSettings
        {
            NvencPath = NvencPath,
            FfprobePath = FfprobePath,
            FfmpegPath = FfmpegPath,
            InputPath = InputPath,
            OutputDirectory = OutputDirectory,
            EnableVsr = EnableVsr,
            AutoVsrResolution = AutoVsrResolution,
            VsrLongEdge = VsrLongEdge,
            VsrResolution = VsrResolution,
            VsrQuality = VsrQuality,
            EnableHdr = EnableHdr,
            HdrContrast = HdrContrast,
            HdrSaturation = HdrSaturation,
            HdrMiddleGray = HdrMiddleGray,
            HdrMaxLuminance = HdrMaxLuminance,
            HdrMaxCll = HdrMaxCll,
            HdrMasterDisplay = HdrMasterDisplay,
            EnableFrameInterpolation = EnableFrameInterpolation,
            TargetFps = TargetFps,
            SkipFrucWhenSourceFpsIsHigh = SkipFrucWhenSourceFpsIsHigh,
            FrucNormalizeMode = FrucNormalizeMode,
            FrucNormalizeFps = FrucNormalizeFps,
            FrucNormalizeCrf = FrucNormalizeCrf,
            FrucNormalizePreset = FrucNormalizePreset,
            KeepTemporaryFiles = KeepTemporaryFiles,
            Deinterlace = Deinterlace,
            Qvbr = Qvbr,
            NvencPreset = NvencPreset,
            BFrames = BFrames,
            TemporalAq = TemporalAq,
            Avsync = Avsync
        };
    }

    private void UseExistingManagedNvenc()
    {
        var existingNvenc = _nvencToolManager.FindExistingNvenc();
        if (existingNvenc is null)
        {
            NvencToolStatus = "NVEnc：未发现内置版本，可点击下载/更新";
            return;
        }

        NvencPath = existingNvenc;
        NvencToolStatus = "NVEnc：已发现 " + existingNvenc;
    }

    private async Task EnsureNvencCapabilitiesAsync(EncodeSettings settings, CancellationToken cancellationToken)
    {
        AppendStatus("检查 NVEncC 功能...");
        var help = await _processRunner.CaptureAsync(
            new ProcessCommand(settings.NvencPath, ["--help"]),
            cancellationToken);
        var helpText = help.CombinedOutput;

        if (settings.EnableVsr && !helpText.Contains("ngx-vsr", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("当前 NVEncC64.exe 不包含 RTX VSR / ngx-vsr 支持。");
        }

        if (settings.EnableHdr && !helpText.Contains("--vpp-ngx-truehdr", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("当前 NVEncC64.exe 不包含 RTX HDR / NGX TrueHDR 支持。");
        }

        if (settings.EnableFrameInterpolation && !helpText.Contains("--vpp-fruc", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("当前 NVEncC64.exe 不包含 FRUC 插帧支持。");
        }

        if (!settings.EnableFrameInterpolation)
        {
            return;
        }

        var features = await _processRunner.CaptureAsync(
            new ProcessCommand(settings.NvencPath, ["--check-features"]),
            cancellationToken);
        if (features.ExitCode != 0)
        {
            AppendStatus("无法读取 --check-features，继续让 NVEncC 在编码时判断 FRUC 可用性。");
            return;
        }

        var featureText = features.CombinedOutput;
        if (!Regex.IsMatch(featureText, "nvof.*fruc.*yes", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            AppendStatus("未确认 NVOF FRUC=yes；如果 GPU 或驱动不支持，编码阶段会失败。");
        }
    }

    private void AppendCommand(ProcessCommand command)
    {
        AppendStatus(command.ToCommandLine());
    }

    private void AppendStatus(string message)
    {
        AppendProcessLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    private void AppendProcessLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        LogText += line + Environment.NewLine;
    }

    private static void TryDeleteTemporaryInput(EncodePlan? plan, EncodeSettings? settings)
    {
        if (plan?.TemporaryInputPath is null || settings?.KeepTemporaryFiles == true)
        {
            return;
        }

        try
        {
            if (File.Exists(plan.TemporaryInputPath))
            {
                File.Delete(plan.TemporaryInputPath);
            }
        }
        catch
        {
        }
    }
}
