using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RTX_Encode_Toolkit.Models;

namespace RTX_Encode_Toolkit.Services;

public sealed class NvencCommandBuilder
{
    private readonly VideoProbeService _videoProbeService;
    private readonly Localization _localization = Localization.Instance;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".webp"
    };

    private static bool IsImageFile(string path) => ImageExtensions.Contains(Path.GetExtension(path));

    public NvencCommandBuilder(VideoProbeService videoProbeService)
    {
        _videoProbeService = videoProbeService;
    }

    public async Task<EncodePlan> BuildAsync(EncodeSettings settings, CancellationToken cancellationToken)
    {
        ValidateSettings(settings);

        var inputPath = Path.GetFullPath(settings.InputPath.Trim('"'));
        var outputDirectory = ResolveOutputDirectory(settings, inputPath);
        var notes = new List<string>();
        VideoInfo? videoInfo = null;
        var isImage = IsImageFile(inputPath);

        if (settings.EnableVsr || (settings.EnableFrameInterpolation && !isImage))
        {
            videoInfo = await _videoProbeService.ProbeAsync(settings.FfprobePath, inputPath, cancellationToken);
        }

        var resolvedVsrResolution = settings.EnableVsr
            ? ResolveVsrResolution(settings, videoInfo)
            : null;
        var outputPath = ResolveOutputPath(settings, inputPath, outputDirectory, resolvedVsrResolution, isImage);

        var encodeInput = inputPath;
        ProcessCommand? preprocessCommand = null;
        ProcessCommand? postprocessCommand = null;
        string? temporaryInputPath = null;
        string? temporaryOutputPath = null;

        var shouldRunFruc = settings.EnableFrameInterpolation && !isImage;
        if (settings.EnableFrameInterpolation && isImage)
        {
            notes.Add(_localization["ImageInputSkippedFruc"]);
        }

        if (shouldRunFruc && videoInfo is not null)
        {
            if (settings.SkipFrucWhenSourceFpsIsHigh && videoInfo.AverageFrameRate >= settings.TargetFps)
            {
                shouldRunFruc = false;
                notes.Add($"源平均帧率 {videoInfo.AverageFrameRate:0.###}fps 已经不低于目标 {settings.TargetFps}fps，已跳过 FRUC。");
            }

            if (videoInfo.AverageFrameRate > 0 && settings.TargetFps / videoInfo.AverageFrameRate > 2.01)
            {
                notes.Add("目标帧率超过源帧率 2 倍，NVOF FRUC 在复杂运动里可能出现闪烁。");
            }

            if (!string.Equals(videoInfo.FieldOrder, "progressive", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(videoInfo.FieldOrder, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                notes.Add($"源 field_order 为 {videoInfo.FieldOrder}，隔行/胶片源建议开启反交错。");
            }

            if (ShouldNormalizeCfr(settings, videoInfo))
            {
                var normalizeFps = settings.FrucNormalizeFps > 0
                    ? settings.FrucNormalizeFps
                    : videoInfo.RealFrameRate > 0 ? videoInfo.RealFrameRate : videoInfo.AverageFrameRate;
                if (normalizeFps <= 0)
                {
                    throw new InvalidOperationException("无法确定 CFR 预处理帧率，请手动填写 CFR 帧率。");
                }

                temporaryInputPath = ResolveTemporaryInputPath(outputDirectory, inputPath, normalizeFps);
                preprocessCommand = BuildNormalizeCommand(settings, inputPath, temporaryInputPath, normalizeFps);
                encodeInput = temporaryInputPath;
                notes.Add($"将先用 ffmpeg 预处理为 CFR {normalizeFps:0.###}fps，再交给 NVEncC 插帧。");
            }
        }

        if (isImage)
        {
            temporaryOutputPath = Path.Combine(outputDirectory, $"{Guid.NewGuid():N}.mkv");
            postprocessCommand = BuildImageExtractCommand(settings, temporaryOutputPath, outputPath);
            notes.Add(_localization["ImageInputCaption"]);
        }

        var mainCommand = BuildNvencCommand(settings, encodeInput, isImage ? temporaryOutputPath! : outputPath, resolvedVsrResolution, shouldRunFruc, isImage);

        return new EncodePlan
        {
            MainCommand = mainCommand,
            PreprocessCommand = preprocessCommand,
            PostprocessCommand = postprocessCommand,
            OutputPath = outputPath,
            TemporaryInputPath = temporaryInputPath,
            TemporaryOutputPath = isImage ? temporaryOutputPath : null,
            ResolvedVsrResolution = resolvedVsrResolution,
            Notes = notes
        };
    }

    public ProcessCommand BuildPreviewCommand(EncodeSettings settings)
    {
        var inputPath = string.IsNullOrWhiteSpace(settings.InputPath) ? "<input>" : settings.InputPath.Trim('"');
        var outputPath = ResolvePreviewOutputPath(settings, inputPath);
        var vsrResolution = settings.EnableVsr
            ? settings.AutoVsrResolution ? $"auto:{settings.VsrLongEdge}x-2/-2x{settings.VsrLongEdge}" : settings.VsrResolution.Trim()
            : null;
        var isImage = !string.IsNullOrWhiteSpace(settings.InputPath) && IsImageFile(settings.InputPath.Trim('"'));
        var includeFruc = settings.EnableFrameInterpolation && !isImage;
        return BuildNvencCommand(settings, inputPath, outputPath, vsrResolution, includeFruc, isImage);
    }

    private static void ValidateSettings(EncodeSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.InputPath))
        {
            throw new InvalidOperationException("请选择输入文件。");
        }

        if (!File.Exists(settings.InputPath.Trim('"')))
        {
            throw new FileNotFoundException("输入文件不存在。", settings.InputPath);
        }

        if (!settings.EnableVsr && !settings.EnableHdr && !settings.EnableFrameInterpolation)
        {
            throw new InvalidOperationException("至少启用 VSR、HDR、插帧中的一项。");
        }

        if (settings.EnableVsr)
        {
            if (settings.AutoVsrResolution && settings.VsrLongEdge < 2)
            {
                throw new InvalidOperationException("VSR 长边必须大于 1。");
            }

            if (!settings.AutoVsrResolution && string.IsNullOrWhiteSpace(settings.VsrResolution))
            {
                throw new InvalidOperationException("请填写 VSR 输出分辨率，例如 3840x-2。");
            }

            if (settings.VsrQuality is < 1 or > 4)
            {
                throw new InvalidOperationException("VSR 质量建议设置为 1 到 4。");
            }
        }

        if (settings.EnableFrameInterpolation && settings.TargetFps <= 0)
        {
            throw new InvalidOperationException("目标帧率必须大于 0。");
        }

        if (settings.Qvbr <= 0)
        {
            throw new InvalidOperationException("QVBR 必须大于 0。");
        }

        if (settings.BFrames < 0)
        {
            throw new InvalidOperationException("B 帧数量不能为负数。");
        }
    }

    private static string ResolveOutputDirectory(EncodeSettings settings, string inputPath)
    {
        if (!string.IsNullOrWhiteSpace(settings.OutputDirectory))
        {
            return Path.GetFullPath(settings.OutputDirectory.Trim('"'));
        }

        var inputDirectory = Path.GetDirectoryName(inputPath);
        if (string.IsNullOrEmpty(inputDirectory))
        {
            inputDirectory = Directory.GetCurrentDirectory();
        }

        return Path.Combine(inputDirectory, "rtx_exports");
    }

    private static string ResolveOutputPath(EncodeSettings settings, string inputPath, string outputDirectory, string? resolvedVsrResolution, bool isImage)
    {
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var tags = new List<string> { baseName };

        if (settings.EnableVsr && !string.IsNullOrWhiteSpace(resolvedVsrResolution))
        {
            tags.Add("rtx_vsr");
            tags.Add(SanitizeFileTag(resolvedVsrResolution));
        }

        if (settings.EnableHdr)
        {
            tags.Add("hdr");
        }

        if (settings.EnableFrameInterpolation && !isImage)
        {
            tags.Add($"fruc{settings.TargetFps}");
        }

        if (!isImage)
        {
            var codecTag = settings.VideoCodec switch
            {
                "AV1" => "av1",
                "H264" => "h264",
                _ => "hevc10"
            };
            tags.Add(codecTag);
            if (settings.EnableHdr)
            {
                tags.Add("hdr10");
            }
        }

        var ext = isImage ? ".png" : ".mkv";
        return Path.Combine(outputDirectory, string.Join("_", tags) + ext);
    }

    private static string ResolvePreviewOutputPath(EncodeSettings settings, string inputPath)
    {
        if (inputPath == "<input>")
        {
            return "<output>";
        }

        var outputDirectory = string.IsNullOrWhiteSpace(settings.OutputDirectory)
            ? Path.Combine(Path.GetDirectoryName(inputPath) ?? ".", "rtx_exports")
            : settings.OutputDirectory.Trim('"');
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(outputDirectory, baseName + "_rtx_export.mkv");
    }

    private static ProcessCommand BuildImageExtractCommand(EncodeSettings settings, string tempMkvPath, string outputPath)
    {
        return new ProcessCommand(
            settings.FfmpegPath,
            new[]
            {
                "-hide_banner",
                "-v", "warning",
                "-stats",
                "-y",
                "-i", tempMkvPath,
                "-vframes", "1",
                "-c:v", "png",
                outputPath
            });
    }

    private static string ResolveVsrResolution(EncodeSettings settings, VideoInfo? videoInfo)
    {
        if (!settings.AutoVsrResolution)
        {
            return settings.VsrResolution.Trim();
        }

        if (videoInfo is null)
        {
            return $"{settings.VsrLongEdge}x-2";
        }

        return videoInfo.DisplayWidth >= videoInfo.DisplayHeight
            ? $"{settings.VsrLongEdge}x-2"
            : $"-2x{settings.VsrLongEdge}";
    }

    private static bool ShouldNormalizeCfr(EncodeSettings settings, VideoInfo videoInfo)
    {
        return settings.FrucNormalizeMode switch
        {
            "Force" => true,
            "Off" => false,
            _ => videoInfo.IsVariableFrameRate
        };
    }

    private static string ResolveTemporaryInputPath(string outputDirectory, string inputPath, double normalizeFps)
    {
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var fpsTag = normalizeFps.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', '_');
        return Path.Combine(outputDirectory, $"{baseName}_cfr{fpsTag}_{Guid.NewGuid():N}.mkv");
    }

    private static ProcessCommand BuildNormalizeCommand(
        EncodeSettings settings,
        string inputPath,
        string temporaryInputPath,
        double normalizeFps)
    {
        var fps = normalizeFps.ToString("0.######", CultureInfo.InvariantCulture);
        return new ProcessCommand(
            settings.FfmpegPath,
            new[]
            {
                "-hide_banner",
                "-v",
                "warning",
                "-stats",
                "-y",
                "-i",
                inputPath,
                "-map",
                "0",
                "-vf",
                $"fps={fps}",
                "-c:v",
                "libx264",
                "-preset",
                settings.FrucNormalizePreset,
                "-crf",
                settings.FrucNormalizeCrf.ToString(CultureInfo.InvariantCulture),
                "-pix_fmt",
                "yuv420p",
                "-c:a",
                "copy",
                "-c:s",
                "copy",
                "-c:d",
                "copy",
                "-c:t",
                "copy",
                "-map_metadata",
                "0",
                "-map_chapters",
                "0",
                "-max_muxing_queue_size",
                "4096",
                temporaryInputPath
            });
    }

    private static ProcessCommand BuildNvencCommand(
        EncodeSettings settings,
        string inputPath,
        string outputPath,
        string? vsrResolution,
        bool includeFruc,
        bool isImage = false)
    {
        var (codec, profile, outputDepth) = settings.VideoCodec switch
        {
            "AV1" => ("av1", "main10", "10"),
            "H264" => ("h264", "high", "8"),
            _ => ("hevc", "main10", "10") // HEVC default
        };

        var args = new List<string>
        {
            isImage ? "--avsw" : "--avhw",
            "-i",
            inputPath,
            "-o",
            outputPath,
            "--codec",
            codec,
            "--profile",
            profile,
            "--output-depth",
            outputDepth,
            "--output-csp",
            "yuv420",
            "--qvbr",
            settings.Qvbr.ToString(CultureInfo.InvariantCulture),
            "--preset",
            settings.NvencPreset,
            "--multipass",
            "2pass-full",
            "--lookahead",
            "32",
            "--lookahead-level",
            "3",
            "--aq"
        };


        if (settings.TemporalAq)
        {
            args.Add("--aq-temporal");
        }

        args.Add("--bframes");
        args.Add(settings.BFrames.ToString(CultureInfo.InvariantCulture));

        if (settings.BFrames > 0)
        {
            args.Add("--bref-mode");
            args.Add("middle");
        }

        if (settings.Deinterlace)
        {
            args.Add("--vpp-deinterlace");
            args.Add("adaptive");
        }

        if (includeFruc)
        {
            args.Add("--vpp-fruc");
            args.Add($"fps={settings.TargetFps}");
        }

        if (settings.EnableVsr && !string.IsNullOrWhiteSpace(vsrResolution))
        {
            args.Add("--output-res");
            args.Add(vsrResolution);
            args.Add("--vpp-resize");
            args.Add($"algo=ngx-vsr,vsr-quality={settings.VsrQuality}");
        }

        if (settings.EnableHdr)
        {
            args.Add("--vpp-ngx-truehdr");
            args.Add($"contrast={settings.HdrContrast},saturation={settings.HdrSaturation},middlegray={settings.HdrMiddleGray},maxluminance={settings.HdrMaxLuminance}");
            args.Add("--colormatrix");
            args.Add("bt2020nc");
            args.Add("--colorprim");
            args.Add("bt2020");
            args.Add("--transfer");
            args.Add("smpte2084");
            args.Add("--max-cll");
            args.Add(settings.HdrMaxCll);
            args.Add("--master-display");
            args.Add(settings.HdrMasterDisplay);
        }
        else
        {
            args.Add("--colormatrix");
            args.Add("auto");
            args.Add("--colorprim");
            args.Add("auto");
            args.Add("--transfer");
            args.Add("auto");
            args.Add("--colorrange");
            args.Add("auto");
        }

        if (!isImage)
        {
            args.AddRange(
            [
                "--audio-copy",
                "--sub-copy",
                "--chapter-copy",
                "--data-copy",
                "--attachment-copy",
                "--metadata",
                "copy",
                "--video-metadata",
                "copy",
                "--avsync",
                settings.Avsync
            ]);
        }

        args.Add("--output-format");
        args.Add("matroska");
        args.Add("--log-level");
        args.Add("info");

        return new ProcessCommand(settings.NvencPath, args);
    }

    private static string SanitizeFileTag(string value)
    {
        return string.Concat(value.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
    }
}
