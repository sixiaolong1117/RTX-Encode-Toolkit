using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RTX_Encode_Toolkit.Models;
using RTX_Encode_Toolkit.Services;

namespace RTX_Encode_Toolkit.ViewModels;

public partial class EncodeTaskViewModel : ViewModelBase
{
    private readonly Localization _localization = Localization.Instance;
    private readonly EncodeTask _task;
    private readonly Action<string> _cancelTask;
    private readonly Action<string> _removeTask;

    public EncodeTaskViewModel(
        EncodeTask task,
        Action<string> cancelTask,
        Action<string> removeTask)
    {
        _task = task;
        _cancelTask = cancelTask;
        _removeTask = removeTask;
        Id = task.Id;
        Settings = task.Settings;
        _task.PropertyChanged += (_, e) => OnTaskPropertyChanged(e.PropertyName);
    }

    public EncodeSettings Settings { get; }

    public string Id { get; }

    // 只读委托，模型 SetProperty 触发 INPC 后经 OnTaskPropertyChanged 转发
    public string Name => _task.Name;
    public EncodeTaskStatus Status => _task.Status;
    public DateTime? StartedAt => _task.StartedAt;
    public DateTime? FinishedAt => _task.FinishedAt;
    public string? OutputPath => _task.OutputPath;
    public string? ErrorMessage => _task.ErrorMessage;
    public string LogText => _task.LogText;
    public string ProgressText => _task.ProgressText;
    public double? ProgressPercent => _task.ProgressPercent;

    // Computed properties for display
    public string StatusText => Status switch
    {
        EncodeTaskStatus.Queued => _localization["StatusQueued"],
        EncodeTaskStatus.Running => _localization["StatusEncoding"],
        EncodeTaskStatus.Cancelling => _localization["StatusCancelling"],
        EncodeTaskStatus.Cancelled => _localization["StatusCancelled"],
        EncodeTaskStatus.Completed => _localization["StatusCompleted"],
        EncodeTaskStatus.Failed => _localization["StatusFailed"],
        _ => Status.ToString()
    };

    public string DurationText
    {
        get
        {
            if (StartedAt is null)
            {
                return string.Empty;
            }

            var end = FinishedAt ?? DateTime.Now;
            var duration = end - StartedAt.Value;
            return duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
                : $"{duration.Minutes:D2}:{duration.Seconds:D2}";
        }
    }

    public bool CanCancel => Status is EncodeTaskStatus.Queued or EncodeTaskStatus.Running;
    public bool CanRemove => Status is EncodeTaskStatus.Queued
        or EncodeTaskStatus.Cancelling
        or EncodeTaskStatus.Cancelled
        or EncodeTaskStatus.Completed
        or EncodeTaskStatus.Failed;
    public bool IsActive => Status is EncodeTaskStatus.Queued or EncodeTaskStatus.Running or EncodeTaskStatus.Cancelling;
    public bool HasProgressText => !string.IsNullOrWhiteSpace(ProgressText);
    public bool HasProgressPercent => ProgressPercent.HasValue;
    public bool IsProgressIndeterminate => IsActive && !HasProgressPercent;
    public double ProgressPercentValue => ProgressPercent ?? 0;
    public bool CanOpenFolder => GetExplorerTarget() is not null;

    public bool HasVsr => Settings.EnableVsr;
    public bool HasHdr => Settings.EnableHdr;
    public bool HasFruc => Settings.EnableFrameInterpolation;

    public string VsrSummary =>
        !Settings.EnableVsr ? string.Empty
        : Settings.AutoVsrResolution
            ? $"VSR Q{Settings.VsrQuality} ({Settings.VsrLongEdge}p)"
            : $"VSR Q{Settings.VsrQuality} ({Settings.VsrResolution})";

    public string HdrSummary =>
        !Settings.EnableHdr ? string.Empty
        : $"HDR C{Settings.HdrContrast} S{Settings.HdrSaturation}";

    public string FrucSummary =>
        !Settings.EnableFrameInterpolation ? string.Empty
        : Settings.FrucNormalizeMode != "Off"
            ? $"FRUC {Settings.TargetFps}fps ({Settings.FrucNormalizeMode})"
            : $"FRUC {Settings.TargetFps}fps";

    public string EncodeSummary =>
        Settings.Deinterlace
            ? $"{Settings.VideoCodec} QVBR={Settings.Qvbr} {Settings.NvencPreset} | Deint"
            : $"{Settings.VideoCodec} QVBR={Settings.Qvbr} {Settings.NvencPreset}";

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cancelTask(Id);
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        _removeTask(Id);
    }

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private void OpenOutput()
    {
        var target = GetExplorerTarget();
        if (target is null)
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = target.SelectFile
                    ? $"/select,\"{target.Path}\""
                    : $"\"{target.Path}\"",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    // ponytail: 模型任意属性变化时通知全部属性+命令状态，省去依赖追踪
    private void OnTaskPropertyChanged(string? propertyName)
    {
        OnPropertyChanged(string.Empty);
        CancelCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        OpenOutputCommand.NotifyCanExecuteChanged();
    }

    private ExplorerTarget? GetExplorerTarget()
    {
        if (!string.IsNullOrWhiteSpace(OutputPath))
        {
            if (File.Exists(OutputPath))
            {
                return new ExplorerTarget(OutputPath, SelectFile: true);
            }

            var outputDirectory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory) && Directory.Exists(outputDirectory))
            {
                return new ExplorerTarget(outputDirectory, SelectFile: false);
            }
        }

        if (!string.IsNullOrWhiteSpace(Settings.OutputDirectory)
            && Directory.Exists(Settings.OutputDirectory))
        {
            return new ExplorerTarget(Settings.OutputDirectory, SelectFile: false);
        }

        if (!string.IsNullOrWhiteSpace(Settings.InputPath))
        {
            if (File.Exists(Settings.InputPath))
            {
                return new ExplorerTarget(Settings.InputPath, SelectFile: true);
            }

            var inputDirectory = Path.GetDirectoryName(Settings.InputPath);
            if (!string.IsNullOrWhiteSpace(inputDirectory) && Directory.Exists(inputDirectory))
            {
                return new ExplorerTarget(inputDirectory, SelectFile: false);
            }
        }

        return null;
    }

    private sealed record ExplorerTarget(string Path, bool SelectFile);
}
