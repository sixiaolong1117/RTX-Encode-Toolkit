using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RTX_Encode_Toolkit.Models;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;

namespace RTX_Encode_Toolkit.Services;

public sealed class NvencToolManager
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/rigaya/NVEnc/releases/latest";
    private const string FixedLatestAssetUrl = "https://github.com/rigaya/NVEnc/releases/latest/download/NVEncC_9.16_x64.7z";
    private const int MaxParallelDownloads = 8;
    private const long ParallelDownloadThresholdBytes = 16L * 1024 * 1024;
    private const long DownloadPartSizeBytes = 8L * 1024 * 1024;
    private static readonly Regex X64AssetNamePattern = new(@"^NVEncC_(?<version>.+)_x64\.7z$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _httpClient;

    public NvencToolManager()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RTX-Encode-Toolkit");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public string ManagedRootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RTX Encode Toolkit",
        "tools",
        "NVEnc");

    public string DownloadDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RTX Encode Toolkit",
        "downloads");

    public string? FindExistingNvenc()
    {
        var portablePath = Path.Combine(AppContext.BaseDirectory, "tools", "NVEnc", "NVEncC64.exe");
        if (File.Exists(portablePath))
        {
            return portablePath;
        }

        if (!Directory.Exists(ManagedRootDirectory))
        {
            return null;
        }

        return Directory.EnumerateFiles(ManagedRootDirectory, "NVEncC64.exe", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public async Task<NvencToolInstallResult> InstallOrUpdateAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ManagedRootDirectory);
        Directory.CreateDirectory(DownloadDirectory);

        var asset = await ResolveLatestAssetAsync(progress, cancellationToken).ConfigureAwait(false);
        var version = ResolveVersionTag(asset);
        var archivePath = Path.Combine(DownloadDirectory, asset.Name);
        var installDirectory = Path.Combine(ManagedRootDirectory, version);

        progress?.Report($"下载：{asset.Name}");
        await DownloadFileAsync(asset.BrowserDownloadUrl, archivePath, asset.Size, progress, cancellationToken).ConfigureAwait(false);

        if (Directory.Exists(installDirectory))
        {
            await Task.Run(() => Directory.Delete(installDirectory, recursive: true), cancellationToken).ConfigureAwait(false);
        }

        Directory.CreateDirectory(installDirectory);
        progress?.Report($"解压到：{installDirectory}");
        await Task.Run(() => ExtractSevenZip(archivePath, installDirectory, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
        progress?.Report("解压完成，查找 NVEncC64.exe...");

        var executablePath = Directory.EnumerateFiles(installDirectory, "NVEncC64.exe", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (executablePath is null)
        {
            throw new FileNotFoundException("解压完成，但没有找到 NVEncC64.exe。");
        }

        progress?.Report($"已安装：{executablePath}");
        return new NvencToolInstallResult(executablePath, installDirectory, version);
    }

    private async Task<GitHubReleaseAsset> ResolveLatestAssetAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            progress?.Report("读取 GitHub latest release...");
            using var response = await _httpClient.GetAsync(LatestReleaseApiUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var asset = release?.Assets
                .Where(item => X64AssetNamePattern.IsMatch(item.Name))
                .OrderByDescending(item => item.Name)
                .FirstOrDefault();

            if (asset is not null && !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            {
                progress?.Report($"GitHub latest: {release?.TagName}, asset: {asset.Name}");
                return asset;
            }

            progress?.Report("GitHub API 未找到 x64 7z asset，改用固定 latest/download URL。");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
            && ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            progress?.Report("GitHub API 读取失败，改用固定 latest/download URL：" + ex.Message);
        }

        return new GitHubReleaseAsset
        {
            Name = "NVEncC_9.16_x64.7z",
            BrowserDownloadUrl = FixedLatestAssetUrl
        };
    }

    private async Task DownloadFileAsync(
        string url,
        string archivePath,
        long expectedSize,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (await TryDownloadFileInParallelAsync(url, archivePath, expectedSize, progress, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        progress?.Report("服务器不支持分段下载，改用单线程下载。");
        await DownloadFileSequentiallyAsync(url, archivePath, expectedSize, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TryDownloadFileInParallelAsync(
        string url,
        string archivePath,
        long expectedSize,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        using var probeRequest = new HttpRequestMessage(HttpMethod.Get, url);
        probeRequest.Headers.Range = new RangeHeaderValue(0, 0);
        using var probeResponse = await _httpClient.SendAsync(probeRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (probeResponse.StatusCode != HttpStatusCode.PartialContent || probeResponse.Content.Headers.ContentRange?.Length is not long totalBytes)
        {
            return false;
        }

        if (totalBytes < ParallelDownloadThresholdBytes)
        {
            return false;
        }

        progress?.Report($"启用 {MaxParallelDownloads} 线程分段下载，总大小 {FormatBytes(totalBytes)}。");
        var temporaryPath = archivePath + ".download";
        var partRanges = CreatePartRanges(totalBytes);
        var downloadedBytes = 0L;
        var reportLock = new object();
        var lastReport = DateTimeOffset.MinValue;
        var indexedRanges = partRanges.Select((range, index) => new DownloadPart(index, range.Start, range.End)).ToArray();

        try
        {
            await Parallel.ForEachAsync(
                indexedRanges,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaxParallelDownloads,
                    CancellationToken = cancellationToken
                },
                async (range, token) => await DownloadPartAsync(
                    url,
                    $"{temporaryPath}.part{range.Index}",
                    range.Start,
                    range.End,
                    value => ReportParallelProgress(value, totalBytes, progress, reportLock, ref downloadedBytes, ref lastReport),
                    token).ConfigureAwait(false)).ConfigureAwait(false);

            await CombinePartsAsync(temporaryPath, partRanges.Count, cancellationToken).ConfigureAwait(false);

            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            File.Move(temporaryPath, archivePath);
            progress?.Report($"下载完成：{archivePath}");
            return true;
        }
        catch
        {
            DeletePartialFiles(temporaryPath, partRanges.Count);
            throw;
        }
    }

    private async Task DownloadFileSequentiallyAsync(
        string url,
        string archivePath,
        long expectedSize,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? expectedSize;
        var temporaryPath = archivePath + ".download";

        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var output = File.Create(temporaryPath))
        {
            var buffer = new byte[1024 * 128];
            long downloadedBytes = 0;
            var lastReport = DateTimeOffset.MinValue;

            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                downloadedBytes += read;

                var now = DateTimeOffset.UtcNow;
                if (now - lastReport > TimeSpan.FromMilliseconds(500))
                {
                    progress?.Report(FormatDownloadProgress(downloadedBytes, totalBytes));
                    lastReport = now;
                }
            }
        }

        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        File.Move(temporaryPath, archivePath);
        progress?.Report($"下载完成：{archivePath}");
    }

    private async Task DownloadPartAsync(
        string url,
        string partPath,
        long start,
        long end,
        Action<int> onBytesRead,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(start, end);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.PartialContent)
        {
            throw new InvalidOperationException($"分段下载失败：服务器返回 {(int)response.StatusCode}。");
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = File.Create(partPath);
        var buffer = new byte[1024 * 128];
        long partBytes = 0;
        var expectedPartBytes = end - start + 1;

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            partBytes += read;
            onBytesRead(read);
        }

        if (partBytes != expectedPartBytes)
        {
            throw new InvalidOperationException($"分段下载大小不匹配：{FormatBytes(partBytes)} / {FormatBytes(expectedPartBytes)}。");
        }
    }

    private static List<(long Start, long End)> CreatePartRanges(long totalBytes)
    {
        var ranges = new List<(long Start, long End)>();
        for (var start = 0L; start < totalBytes; start += DownloadPartSizeBytes)
        {
            var end = Math.Min(start + DownloadPartSizeBytes - 1, totalBytes - 1);
            ranges.Add((start, end));
        }

        return ranges;
    }

    private static async Task CombinePartsAsync(string temporaryPath, int partCount, CancellationToken cancellationToken)
    {
        await using var output = File.Create(temporaryPath);
        var buffer = new byte[1024 * 1024];

        for (var index = 0; index < partCount; index++)
        {
            var partPath = $"{temporaryPath}.part{index}";
            await using (var input = File.OpenRead(partPath))
            {
                while (true)
                {
                    var read = await input.ReadAsync(buffer, cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
            }

            File.Delete(partPath);
        }
    }

    private static void ReportParallelProgress(
        int bytesRead,
        long totalBytes,
        IProgress<string>? progress,
        object reportLock,
        ref long downloadedBytes,
        ref DateTimeOffset lastReport)
    {
        var currentBytes = Interlocked.Add(ref downloadedBytes, bytesRead);
        var now = DateTimeOffset.UtcNow;

        lock (reportLock)
        {
            if (now - lastReport <= TimeSpan.FromMilliseconds(500) && currentBytes < totalBytes)
            {
                return;
            }

            progress?.Report(FormatDownloadProgress(currentBytes, totalBytes));
            lastReport = now;
        }
    }

    private static void DeletePartialFiles(string temporaryPath, int partCount)
    {
        TryDeleteFile(temporaryPath);
        for (var index = 0; index < partCount; index++)
        {
            TryDeleteFile($"{temporaryPath}.part{index}");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void ExtractSevenZip(
        string archivePath,
        string installDirectory,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var normalizedInstallDirectory = Path.GetFullPath(installDirectory);
        using var archive = SevenZipArchive.Open(archivePath);
        var extractedFiles = 0;

        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entryPath = entry.Key;
            if (string.IsNullOrWhiteSpace(entryPath))
            {
                continue;
            }

            var destinationPath = Path.GetFullPath(Path.Combine(normalizedInstallDirectory, entryPath));
            if (!destinationPath.StartsWith(normalizedInstallDirectory, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"压缩包内路径无效：{entryPath}");
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            using (var input = entry.OpenEntryStream())
            using (var output = File.Create(destinationPath))
            {
                input.CopyTo(output);
            }

            extractedFiles++;
            if (extractedFiles % 20 == 0)
            {
                progress?.Report($"已解压 {extractedFiles} 个文件...");
            }
        }

        progress?.Report($"已解压 {extractedFiles} 个文件。");
    }

    private static string ResolveVersionTag(GitHubReleaseAsset asset)
    {
        var match = X64AssetNamePattern.Match(asset.Name);
        if (match.Success)
        {
            return match.Groups["version"].Value;
        }

        return "latest";
    }

    private static string FormatDownloadProgress(long downloadedBytes, long totalBytes)
    {
        if (totalBytes <= 0)
        {
            return $"已下载 {FormatBytes(downloadedBytes)}";
        }

        var percent = downloadedBytes * 100d / totalBytes;
        return $"已下载 {percent:0.0}% ({FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes)})";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private readonly record struct DownloadPart(int Index, long Start, long End);
}
