using System;
using System.IO;

namespace RTX_Encode_Toolkit.Services;

public static class ToolPathResolver
{
    public static ToolPathLookupResult Resolve(string path)
    {
        var normalizedPath = path.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return new ToolPathLookupResult(false, ToolPathLookupStatus.Unset, null);
        }

        if (!HasDirectorySeparator(normalizedPath))
        {
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
            foreach (var dir in paths)
            {
                try
                {
                    var candidate = Path.Combine(dir.Trim(), normalizedPath);
                    if (File.Exists(candidate))
                    {
                        return new ToolPathLookupResult(true, ToolPathLookupStatus.FoundInPath, candidate);
                    }
                }
                catch
                {
                }
            }

            return new ToolPathLookupResult(false, ToolPathLookupStatus.NotFoundInPath, null);
        }

        return File.Exists(normalizedPath)
            ? new ToolPathLookupResult(true, ToolPathLookupStatus.FoundAtPath, Path.GetFullPath(normalizedPath))
            : new ToolPathLookupResult(false, ToolPathLookupStatus.FileMissing, null);
    }

    public static bool IsPathValid(string path)
    {
        return Resolve(path).IsFound;
    }

    public static string GetPathHint(string path)
    {
        var result = Resolve(path);
        return result.Status switch
        {
            ToolPathLookupStatus.FoundAtPath => $"✓ {result.ResolvedPath}",
            ToolPathLookupStatus.FoundInPath => $"PATH: {result.ResolvedPath}",
            ToolPathLookupStatus.NotFoundInPath => "未找到",
            ToolPathLookupStatus.FileMissing => "文件不存在",
            _ => "未设置"
        };
    }

    private static bool HasDirectorySeparator(string path)
    {
        return path.Contains(Path.DirectorySeparatorChar)
            || path.Contains(Path.AltDirectorySeparatorChar);
    }
}

public enum ToolPathLookupStatus
{
    Unset,
    FoundAtPath,
    FoundInPath,
    NotFoundInPath,
    FileMissing
}

public sealed record ToolPathLookupResult(bool IsFound, ToolPathLookupStatus Status, string? ResolvedPath);
