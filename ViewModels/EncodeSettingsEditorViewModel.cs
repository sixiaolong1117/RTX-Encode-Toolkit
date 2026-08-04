using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using RTX_Encode_Toolkit.Models;
using RTX_Encode_Toolkit.Services;

namespace RTX_Encode_Toolkit.ViewModels;

public partial class EncodeSettingsEditorViewModel : ViewModelBase
{
    private readonly Localization _localization = Localization.Instance;
    private readonly NvencCommandBuilder? _commandBuilder;
    private bool _isUpdatingInputPaths;
    private List<string> _inputPaths = [];

    public EncodeSettingsEditorViewModel()
    {
    }

    public EncodeSettingsEditorViewModel(NvencCommandBuilder commandBuilder)
    {
        _commandBuilder = commandBuilder;
    }

    // Tool paths (synced from MainWindowViewModel)
    [ObservableProperty]
    private string _nvencPath = "NVEncC64.exe";

    [ObservableProperty]
    private string _ffprobePath = "ffprobe.exe";

    [ObservableProperty]
    private string _ffmpegPath = "ffmpeg.exe";

    // File selection
    [ObservableProperty]
    private string _inputPath = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    // VSR settings
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

    // HDR settings
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

    // FRUC settings
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

    // Encode settings
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
    private string _videoCodec = "HEVC";

    // Static option lists
    public IReadOnlyList<string> VideoCodecs { get; } = ["HEVC", "AV1", "H264"];
    public IReadOnlyList<string> NvencPresets { get; } = ["P7", "P6", "P5", "P4", "P3", "P2", "P1"];
    public IReadOnlyList<string> AvsyncModes { get; } = ["auto", "forcecfr", "vfr"];
    public IReadOnlyList<string> NormalizeModes { get; } = ["Auto", "Force", "Off"];
    public IReadOnlyList<string> NormalizePresets { get; } =
        ["ultrafast", "superfast", "veryfast", "faster", "fast", "medium"];

    public IReadOnlyList<string> InputPaths => _inputPaths;
    public bool HasInputPaths => _inputPaths.Count > 0;

    public string CommandPreview
    {
        get
        {
            if (_commandBuilder is null)
            {
                return string.Empty;
            }

            var settings = CreateSnapshot(GetPrimaryInputPath());
            var builder = new StringBuilder();
            if (settings.EnableFrameInterpolation && settings.FrucNormalizeMode != "Off")
            {
                builder.AppendLine(_localization["FfmpegCfrPrepass"]);
                builder.AppendLine();
            }

            builder.Append(_commandBuilder.BuildPreviewCommand(settings).ToCommandLine());
            return builder.ToString();
        }
    }

    /// <summary>
    /// Creates an immutable snapshot of current settings for adding to queue.
    /// </summary>
    public EncodeSettings CreateSnapshot()
    {
        return CreateSnapshot(GetPrimaryInputPath());
    }

    public EncodeSettings CreateSnapshot(string inputPath)
    {
        return new EncodeSettings
        {
            NvencPath = NvencPath,
            FfprobePath = FfprobePath,
            FfmpegPath = FfmpegPath,
            InputPath = inputPath,
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
            VideoCodec = VideoCodec,
            Qvbr = Qvbr,
            NvencPreset = NvencPreset,
            BFrames = BFrames,
            TemporalAq = TemporalAq,
            Avsync = Avsync
        };
    }

    public void SetInputPaths(IEnumerable<string> inputPaths)
    {
        UpdateInputPaths(inputPaths);

        _isUpdatingInputPaths = true;
        try
        {
            InputPath = string.Join(Environment.NewLine, _inputPaths);
        }
        finally
        {
            _isUpdatingInputPaths = false;
        }
    }

    partial void OnInputPathChanged(string value)
    {
        if (_isUpdatingInputPaths)
        {
            return;
        }

        UpdateInputPaths(ParseInputPaths(value));
    }

    /// <summary>
    /// Raises property changed notification for CommandPreview.
    /// </summary>
    public void RefreshCommandPreview()
    {
        OnPropertyChanged(nameof(CommandPreview));
    }

    private string GetPrimaryInputPath()
    {
        return _inputPaths.FirstOrDefault() ?? InputPath;
    }

    private void UpdateInputPaths(IEnumerable<string> inputPaths)
    {
        _inputPaths = inputPaths
            .Select(path => path.Trim())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        OnPropertyChanged(nameof(InputPaths));
        OnPropertyChanged(nameof(HasInputPaths));
    }

    private static IEnumerable<string> ParseInputPaths(string value)
    {
        return value.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
