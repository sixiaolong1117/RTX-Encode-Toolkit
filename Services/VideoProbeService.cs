using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RTX_Encode_Toolkit.Models;

namespace RTX_Encode_Toolkit.Services;

public sealed class VideoProbeService
{
    private readonly ProcessRunner _processRunner;

    public VideoProbeService(ProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<VideoInfo> ProbeAsync(string ffprobePath, string inputPath, CancellationToken cancellationToken)
    {
        var command = new ProcessCommand(
            ffprobePath,
            new[]
            {
                "-v",
                "error",
                "-select_streams",
                "v:0",
                "-show_entries",
                "stream=width,height,avg_frame_rate,r_frame_rate,field_order:stream_tags=rotate:stream_side_data=rotation",
                "-of",
                "json",
                inputPath
            });

        var result = await _processRunner.CaptureAsync(command, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("ffprobe 读取视频信息失败：" + result.CombinedOutput.Trim());
        }

        using var document = JsonDocument.Parse(result.StandardOutput);
        if (!document.RootElement.TryGetProperty("streams", out var streams)
            || streams.ValueKind != JsonValueKind.Array
            || streams.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("ffprobe 没有找到视频流。");
        }

        var stream = streams[0];
        var width = GetInt32(stream, "width");
        var height = GetInt32(stream, "height");
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("ffprobe 返回的视频宽高无效。");
        }

        return new VideoInfo
        {
            Width = width,
            Height = height,
            Rotation = ReadRotation(stream),
            AverageFrameRate = ParseFrameRate(GetString(stream, "avg_frame_rate")),
            RealFrameRate = ParseFrameRate(GetString(stream, "r_frame_rate")),
            FieldOrder = GetString(stream, "field_order") ?? "unknown"
        };
    }

    private static double ReadRotation(JsonElement stream)
    {
        if (stream.TryGetProperty("tags", out var tags)
            && tags.TryGetProperty("rotate", out var tagRotation)
            && TryGetDouble(tagRotation, out var rotationFromTag))
        {
            return rotationFromTag;
        }

        if (stream.TryGetProperty("side_data_list", out var sideDataList)
            && sideDataList.ValueKind == JsonValueKind.Array)
        {
            foreach (var sideData in sideDataList.EnumerateArray())
            {
                if (sideData.TryGetProperty("rotation", out var sideDataRotation)
                    && TryGetDouble(sideDataRotation, out var rotationFromSideData))
                {
                    return rotationFromSideData;
                }
            }
        }

        return 0;
    }

    private static int GetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) => value,
            _ => 0
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }

    private static bool TryGetDouble(JsonElement element, out double value)
    {
        value = 0;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDouble(out value),
            JsonValueKind.String => double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value),
            _ => false
        };
    }

    private static double ParseFrameRate(string? rate)
    {
        if (string.IsNullOrWhiteSpace(rate) || rate == "0/0")
        {
            return 0;
        }

        var parts = rate.Split('/', 2);
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator)
            && denominator != 0)
        {
            return numerator / denominator;
        }

        return double.TryParse(rate, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
