using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RTX_Encode_Toolkit.Models;

namespace RTX_Encode_Toolkit.Services;

public sealed class EncodeTaskRunner
{
    private static readonly Regex PercentRegex = new(
        @"(?<![\d.])(?<percent>\d{1,3}(?:\.\d+)?)\s*%",
        RegexOptions.Compiled);

    private readonly ProcessRunner _processRunner;
    private readonly NvencCommandBuilder _commandBuilder;
    private readonly Localization _localization = Localization.Instance;

    public EncodeTaskRunner(ProcessRunner processRunner, NvencCommandBuilder commandBuilder)
    {
        _processRunner = processRunner;
        _commandBuilder = commandBuilder;
    }

    public async Task RunAsync(
        EncodeTask task,
        IProgress<EncodeTaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        EncodePlan? plan = null;

        try
        {
            var settings = task.Settings;
            task.Status = EncodeTaskStatus.Running;
            task.StartedAt = DateTime.Now;
            Report(progress, new EncodeTaskProgress(task.Id, Status: EncodeTaskStatus.Running));

            AppendLog(task, _localization["ParsingVideoInfo"], progress);
            plan = await _commandBuilder.BuildAsync(settings, cancellationToken);
            task.OutputPath = plan.OutputPath;
            Report(progress, new EncodeTaskProgress(task.Id, OutputPath: plan.OutputPath));

            foreach (var note in plan.Notes)
            {
                AppendLog(task, note, progress);
            }

            var outputDirectory = Path.GetDirectoryName(plan.OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await EnsureNvencCapabilitiesAsync(task, settings, progress, cancellationToken);

            if (plan.PreprocessCommand is not null)
            {
                AppendLog(task, _localization["RunningFfmpegPreprocess"], progress);
                AppendCommand(task, plan.PreprocessCommand, progress);
                var prepassExitCode = await _processRunner.RunAsync(
                    plan.PreprocessCommand,
                    new Progress<string>(line => AppendProcessLine(task, line, progress)),
                    cancellationToken);
                if (prepassExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"{_localization["FfmpegPreprocessFailed"]}{prepassExitCode}。");
                }
            }

            AppendLog(task, _localization["RunningNvencEncode"], progress);
            AppendCommand(task, plan.MainCommand, progress);
            var exitCode = await _processRunner.RunAsync(
                plan.MainCommand,
                new Progress<string>(line => AppendProcessLine(task, line, progress)),
                cancellationToken);
            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"{_localization["NvencEncodeFailed"]}{exitCode}。");
            }

            if (plan.PostprocessCommand is not null)
            {
                AppendLog(task, _localization["RunningFfmpegExtract"], progress);
                AppendCommand(task, plan.PostprocessCommand, progress);
                var extractExitCode = await _processRunner.RunAsync(
                    plan.PostprocessCommand,
                    new Progress<string>(line => AppendProcessLine(task, line, progress)),
                    cancellationToken);
                if (extractExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"{_localization["FfmpegExtractFailed"]}{extractExitCode}。");
                }
            }

            task.Status = EncodeTaskStatus.Completed;
            task.FinishedAt = DateTime.Now;
            task.ProgressPercent = 100;
            task.ProgressText = _localization["StatusCompleted"];
            AppendLog(task, _localization["CompletedOutput"] + plan.OutputPath, progress);
            Report(progress, new EncodeTaskProgress(
                task.Id,
                Status: EncodeTaskStatus.Completed));
        }
        catch (OperationCanceledException)
        {
            task.Status = EncodeTaskStatus.Cancelled;
            task.FinishedAt = DateTime.Now;
            task.ProgressText = _localization["TaskCancelled"];
            AppendLog(task, _localization["TaskCancelled"], progress);
            Report(progress, new EncodeTaskProgress(
                task.Id,
                Status: EncodeTaskStatus.Cancelled));
        }
        catch (Exception ex)
        {
            task.Status = EncodeTaskStatus.Failed;
            task.FinishedAt = DateTime.Now;
            task.ErrorMessage = ex.Message;
            task.ProgressText = ex.Message;
            AppendLog(task, ex.Message, progress);
            Report(progress, new EncodeTaskProgress(
                task.Id,
                Status: EncodeTaskStatus.Failed,
                ErrorMessage: ex.Message));
            throw;
        }
        finally
        {
            TryDeleteTemporaryInput(plan, task.Settings);
        }
    }

    private async Task EnsureNvencCapabilitiesAsync(
        EncodeTask task,
        EncodeSettings settings,
        IProgress<EncodeTaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        AppendLog(task, _localization["CheckingNvencCapabilities"], progress);
        var help = await _processRunner.CaptureAsync(
            new ProcessCommand(settings.NvencPath, ["--help"]),
            cancellationToken);
        var helpText = help.CombinedOutput;

        if (settings.EnableVsr && !helpText.Contains("ngx-vsr", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(_localization["NvencNoVsr"]);
        }

        if (settings.EnableHdr && !helpText.Contains("--vpp-ngx-truehdr", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(_localization["NvencNoHdr"]);
        }

        if (settings.EnableFrameInterpolation && !helpText.Contains("--vpp-fruc", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(_localization["NvencNoFruc"]);
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
            AppendLog(task, _localization["NvencCheckFeaturesFailed"], progress);
            return;
        }

        var featureText = features.CombinedOutput;
        if (!Regex.IsMatch(featureText, "nvof.*fruc.*yes", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            AppendLog(task, _localization["NvencFrucNotConfirmed"], progress);
        }
    }

    private static void AppendCommand(
        EncodeTask task,
        ProcessCommand command,
        IProgress<EncodeTaskProgress>? progress)
    {
        AppendLog(task, command.ToCommandLine(), progress);
    }

    private static void AppendLog(
        EncodeTask task,
        string message,
        IProgress<EncodeTaskProgress>? progress)
    {
        AppendProcessLine(task, $"[{DateTime.Now:HH:mm:ss}] {message}", progress);
    }

    private static void AppendProcessLine(
        EncodeTask task,
        string line,
        IProgress<EncodeTaskProgress>? progress)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var trimmedLine = line.TrimEnd();
        task.LogText += trimmedLine + Environment.NewLine;

        if (TryUpdateProgress(task, trimmedLine))
        {
            // 进度字段已直接写入模型并经 INPC 通知，快照只需同步日志
            Report(progress, new EncodeTaskProgress(
                task.Id,
                LogText: task.LogText));
            return;
        }

        Report(progress, new EncodeTaskProgress(task.Id, LogText: task.LogText));
    }

    private static bool TryUpdateProgress(EncodeTask task, string line)
    {
        var compactLine = Regex.Replace(line, @"\s+", " ").Trim();
        if (compactLine.Length == 0)
        {
            return false;
        }

        if (compactLine.Length > 180)
        {
            compactLine = compactLine[..180] + "...";
        }

        var percentMatch = PercentRegex.Match(compactLine);
        if (percentMatch.Success
            && double.TryParse(
                percentMatch.Groups["percent"].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var percent)
            && percent is >= 0 and <= 100)
        {
            task.ProgressPercent = percent;
            task.ProgressText = compactLine;
            return true;
        }

        if (compactLine.Contains("frame=", StringComparison.OrdinalIgnoreCase)
            || compactLine.Contains("fps=", StringComparison.OrdinalIgnoreCase)
            || compactLine.Contains("time=", StringComparison.OrdinalIgnoreCase)
            || compactLine.Contains("bitrate=", StringComparison.OrdinalIgnoreCase)
            || compactLine.Contains("speed=", StringComparison.OrdinalIgnoreCase))
        {
            task.ProgressText = compactLine;
            return true;
        }

        return false;
    }

    private static void TryDeleteTemporaryInput(EncodePlan? plan, EncodeSettings settings)
    {
        if (settings.KeepTemporaryFiles)
        {
            return;
        }

        TryDeleteFile(plan?.TemporaryInputPath);
        TryDeleteFile(plan?.TemporaryOutputPath);
    }

    private static void TryDeleteFile(string? path)
    {
        if (path is null)
        {
            return;
        }

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

    private static void Report(IProgress<EncodeTaskProgress>? progress, EncodeTaskProgress value)
    {
        progress?.Report(value);
    }
}
