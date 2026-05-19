namespace RTX_Encode_Toolkit.Models;

public sealed class EncodeSettings
{
    public string NvencPath { get; init; } = "NVEncC64.exe";

    public string FfprobePath { get; init; } = "ffprobe.exe";

    public string FfmpegPath { get; init; } = "ffmpeg.exe";

    public string InputPath { get; init; } = string.Empty;

    public string OutputDirectory { get; init; } = string.Empty;

    public bool EnableVsr { get; init; } = true;

    public bool AutoVsrResolution { get; init; } = true;

    public int VsrLongEdge { get; init; } = 3840;

    public string VsrResolution { get; init; } = "3840x-2";

    public int VsrQuality { get; init; } = 4;

    public bool EnableHdr { get; init; }

    public int HdrContrast { get; init; } = 125;

    public int HdrSaturation { get; init; } = 75;

    public int HdrMiddleGray { get; init; } = 44;

    public int HdrMaxLuminance { get; init; } = 1000;

    public string HdrMaxCll { get; init; } = "1000,400";

    public string HdrMasterDisplay { get; init; } = "G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,1)";

    public bool EnableFrameInterpolation { get; init; }

    public int TargetFps { get; init; } = 120;

    public bool SkipFrucWhenSourceFpsIsHigh { get; init; } = true;

    public string FrucNormalizeMode { get; init; } = "Auto";

    public double FrucNormalizeFps { get; init; } = 0;

    public int FrucNormalizeCrf { get; init; } = 10;

    public string FrucNormalizePreset { get; init; } = "veryfast";

    public bool KeepTemporaryFiles { get; init; }

    public bool Deinterlace { get; init; }

    public int Qvbr { get; init; } = 20;

    public string NvencPreset { get; init; } = "P7";

    public int BFrames { get; init; } = 5;

    public bool TemporalAq { get; init; } = true;

    public string Avsync { get; init; } = "auto";
}
