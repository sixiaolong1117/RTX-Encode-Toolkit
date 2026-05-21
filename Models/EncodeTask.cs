using System;

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
    string? ProgressText = null,
    double? ProgressPercent = null,
    string? OutputPath = null,
    string? ErrorMessage = null);

public sealed class EncodeTask
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required EncodeSettings Settings { get; init; }
    public string Name { get; set; } = "Task";
    public EncodeTaskStatus Status { get; set; } = EncodeTaskStatus.Queued;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? OutputPath { get; set; }
    public string? ErrorMessage { get; set; }
    public string LogText { get; set; } = string.Empty;
    public string ProgressText { get; set; } = string.Empty;
    public double? ProgressPercent { get; set; }
    public int SortOrder { get; set; }
}
