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
        Action<string>? cancelTask = null,
        Action<string>? removeTask = null)
    {
        _task = task;
        _cancelTask = cancelTask ?? (_ => { });
        _removeTask = removeTask ?? (_ => { });
        Id = task.Id;
        Name = task.Name;
        Settings = task.Settings;
        Status = task.Status;
        StartedAt = task.StartedAt;
        FinishedAt = task.FinishedAt;
        OutputPath = task.OutputPath;
        ErrorMessage = task.ErrorMessage;
        LogText = task.LogText;
        ProgressText = task.ProgressText;
        ProgressPercent = task.ProgressPercent;
    }

    public EncodeTask Task => _task;
    public EncodeSettings Settings { get; }

    public string Id { get; }

    public string Name
    {
        get => _task.Name;
        set
        {
            if (_task.Name != value)
            {
                _task.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public EncodeTaskStatus Status
    {
        get => _task.Status;
        set
        {
            if (_task.Status != value)
            {
                _task.Status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(CanRemove));
                OnPropertyChanged(nameof(CanOpenFolder));
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(IsProgressIndeterminate));
                CancelCommand.NotifyCanExecuteChanged();
                RemoveCommand.NotifyCanExecuteChanged();
                OpenOutputCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public DateTime? StartedAt
    {
        get => _task.StartedAt;
        set
        {
            _task.StartedAt = value;
            OnPropertyChanged();
        }
    }

    public DateTime? FinishedAt
    {
        get => _task.FinishedAt;
        set
        {
            _task.FinishedAt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DurationText));
        }
    }

    public string? OutputPath
    {
        get => _task.OutputPath;
        set
        {
            _task.OutputPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanOpenFolder));
            OpenOutputCommand.NotifyCanExecuteChanged();
        }
    }

    public string? ErrorMessage
    {
        get => _task.ErrorMessage;
        set
        {
            _task.ErrorMessage = value;
            OnPropertyChanged();
        }
    }

    public string LogText
    {
        get => _task.LogText;
        set
        {
            _task.LogText = value;
            OnPropertyChanged();
        }
    }

    public string ProgressText
    {
        get => _task.ProgressText;
        set
        {
            _task.ProgressText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasProgressText));
        }
    }

    public double? ProgressPercent
    {
        get => _task.ProgressPercent;
        set
        {
            _task.ProgressPercent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressPercentValue));
            OnPropertyChanged(nameof(HasProgressPercent));
            OnPropertyChanged(nameof(IsProgressIndeterminate));
        }
    }

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

    public void NotifyPropertyChanged(string propertyName)
    {
        OnPropertyChanged(propertyName);
    }

    public void NotifyCommandStateChanged()
    {
        CancelCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        OpenOutputCommand.NotifyCanExecuteChanged();
    }

    private sealed record ExplorerTarget(string Path, bool SelectFile);
}
