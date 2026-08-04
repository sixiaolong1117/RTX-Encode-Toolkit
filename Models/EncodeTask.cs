using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RTX_Encode_Toolkit.Models;

public enum EncodeTaskStatus
{
    Queued,
    Running,
    Cancelling,
    Cancelled,
    Completed,
    Failed
}

public sealed record EncodeTaskProgress(
    string TaskId,
    EncodeTaskStatus? Status = null,
    string? LogText = null,
    string? OutputPath = null,
    string? ErrorMessage = null);

public sealed class EncodeTask : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required EncodeSettings Settings { get; init; }

    private string _name = "Task";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private EncodeTaskStatus _status = EncodeTaskStatus.Queued;
    public EncodeTaskStatus Status { get => _status; set => SetProperty(ref _status, value); }

    private DateTime? _startedAt;
    public DateTime? StartedAt { get => _startedAt; set => SetProperty(ref _startedAt, value); }

    private DateTime? _finishedAt;
    public DateTime? FinishedAt { get => _finishedAt; set => SetProperty(ref _finishedAt, value); }

    private string? _outputPath;
    public string? OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }

    private string? _errorMessage;
    public string? ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

    private string _logText = string.Empty;
    public string LogText { get => _logText; set => SetProperty(ref _logText, value); }

    private string _progressText = string.Empty;
    public string ProgressText { get => _progressText; set => SetProperty(ref _progressText, value); }

    private double? _progressPercent;
    public double? ProgressPercent { get => _progressPercent; set => SetProperty(ref _progressPercent, value); }

    public int SortOrder { get; set; }
}
