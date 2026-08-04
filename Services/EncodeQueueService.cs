using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RTX_Encode_Toolkit.Models;

namespace RTX_Encode_Toolkit.Services;

public sealed class EncodeQueueService
{
    private readonly EncodeTaskRunner _runner;
    private readonly Localization _localization = Localization.Instance;

    private readonly object _lock = new();
    private readonly Queue<EncodeTask> _pendingQueue = new();
    private EncodeTask? _currentTask;
    private CancellationTokenSource? _currentCts;
    private bool _isProcessing;

    private int _nextSortOrder;

    public EncodeQueueService(EncodeTaskRunner runner)
    {
        _runner = runner;
    }

    /// <summary>
    /// All tasks (pending + running + completed/failed/cancelled) in display order.
    /// </summary>
    public List<EncodeTask> Tasks { get; } = new();

    public IReadOnlyList<EncodeTask> GetTasksSnapshot()
    {
        lock (_lock)
        {
            return Tasks.OrderBy(task => task.SortOrder).ToList();
        }
    }

    /// <summary>
    /// Fires when any task status or property changes (UI should refresh list).
    /// </summary>
    public event Action? TasksChanged;

    /// <summary>
    /// Enqueues a task. If the queue was empty, processing starts immediately.
    /// </summary>
    public void Enqueue(EncodeTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        lock (_lock)
        {
            task.SortOrder = ++_nextSortOrder;
            Tasks.Add(task);
            _pendingQueue.Enqueue(task);
        }

        TasksChanged?.Invoke();
        TryDequeueAndRun();
    }

    /// <summary>
    /// Requests cancellation for a specific task. If it's currently running, the
    /// cancellation token is signalled. If it's pending, it is removed silently.
    /// </summary>
    public void CancelTask(string taskId)
    {
        EncodeTask? target = null;
        CancellationTokenSource? ctsToCancel = null;

        lock (_lock)
        {
            target = Tasks.Find(t => t.Id == taskId);
            if (target is null)
            {
                return;
            }

            if (target.Status is EncodeTaskStatus.Completed
                or EncodeTaskStatus.Cancelled
                or EncodeTaskStatus.Failed)
            {
                return;
            }

            if (target == _currentTask)
            {
                target.Status = EncodeTaskStatus.Cancelling;
                ctsToCancel = _currentCts;
            }
            else
            {
                // Still pending — remove from queue
                target.Status = EncodeTaskStatus.Cancelled;
                target.FinishedAt = DateTime.Now;
                AppendLog(target, _localization["TaskCancelled"]);
                // We cannot easily remove from Queue<T>, so we'll skip it when dequeuing.
            }
        }

        ctsToCancel?.Cancel();
        TasksChanged?.Invoke();
    }

    public void RemoveTask(string taskId)
    {
        var removed = false;
        CancellationTokenSource? ctsToCancel = null;

        lock (_lock)
        {
            var target = Tasks.Find(t => t.Id == taskId);
            if (target is null)
            {
                return;
            }

            if (target == _currentTask
                && target.Status is EncodeTaskStatus.Running or EncodeTaskStatus.Cancelling)
            {
                target.Status = EncodeTaskStatus.Cancelling;
                ctsToCancel = _currentCts;
            }
            else if (target.Status is EncodeTaskStatus.Queued)
            {
                target.Status = EncodeTaskStatus.Cancelled;
                target.FinishedAt = DateTime.Now;
                AppendLog(target, _localization["TaskCancelled"]);
            }
            else if (target.Status is not (EncodeTaskStatus.Completed
                or EncodeTaskStatus.Cancelled
                or EncodeTaskStatus.Failed
                or EncodeTaskStatus.Cancelling))
            {
                return;
            }

            removed = Tasks.Remove(target);
        }

        ctsToCancel?.Cancel();

        if (removed)
        {
            TasksChanged?.Invoke();
        }
    }

    /// <summary>
    /// Cancels all pending and running tasks.
    /// </summary>
    public void CancelAllTasks()
    {
        CancellationTokenSource? cts;
        lock (_lock)
        {
            cts = _currentCts;

            foreach (var task in Tasks)
            {
                if (task.Status is EncodeTaskStatus.Queued)
                {
                    task.Status = EncodeTaskStatus.Cancelled;
                    task.FinishedAt = DateTime.Now;
                    AppendLog(task, _localization["TaskCancelled"]);
                }
                else if (task.Status is EncodeTaskStatus.Running or EncodeTaskStatus.Cancelling)
                {
                    task.Status = EncodeTaskStatus.Cancelling;
                }
            }

            _pendingQueue.Clear();
        }

        cts?.Cancel();
        TasksChanged?.Invoke();
    }

    /// <summary>
    /// Removes all completed / failed / cancelled tasks from the list.
    /// </summary>
    public bool AnyActiveTasks()
    {
        lock (_lock)
        {
            return Tasks.Any(t => t.Status is EncodeTaskStatus.Queued
                or EncodeTaskStatus.Running
                or EncodeTaskStatus.Cancelling);
        }
    }

    private void TryDequeueAndRun()
    {
        lock (_lock)
        {
            if (_isProcessing)
            {
                return;
            }

            _isProcessing = true;
        }

        // Fire-and-forget; exceptions surfaced via task status.
        _ = DequeueAndRunAsync();
    }

    private async Task DequeueAndRunAsync()
    {
        EncodeTask? task;
        CancellationTokenSource cts;

        while (true)
        {
            lock (_lock)
            {
                // Dequeue next valid pending task (skip previously cancelled ones)
                task = DequeueNextValid();
                if (task is null)
                {
                    _currentTask = null;
                    _currentCts = null;
                    _isProcessing = false;
                    return;
                }

                _currentTask = task;
                _currentCts = cts = new CancellationTokenSource();
            }

            try
            {
                await _runner.RunAsync(
                    task,
                    new Progress<EncodeTaskProgress>(ApplyProgress),
                    cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Already handled inside RunAsync (sets status to Cancelled)
            }
            catch (Exception)
            {
                // Already handled inside RunAsync (sets status to Failed)
            }
            finally
            {
                lock (_lock)
                {
                    if (_currentTask == task)
                    {
                        _currentTask = null;
                        _currentCts = null;
                    }
                }

                cts.Dispose();
                TasksChanged?.Invoke();
            }
        }
    }

    private void ApplyProgress(EncodeTaskProgress progress)
    {
        lock (_lock)
        {
            var task = Tasks.Find(t => t.Id == progress.TaskId);
            if (task is null)
            {
                return;
            }

            if (progress.Status.HasValue)
            {
                task.Status = progress.Status.Value;
            }

            if (progress.OutputPath is not null)
            {
                task.OutputPath = progress.OutputPath;
            }

            if (progress.ErrorMessage is not null)
            {
                task.ErrorMessage = progress.ErrorMessage;
            }

            if (progress.LogText is not null && progress.LogText.Length >= task.LogText.Length)
            {
                task.LogText = progress.LogText;
            }
            // ProgressText/ProgressPercent 由 AppendProcessLine 直接写模型并经 INPC 通知，
            // 快照覆盖会造成异步投递下的瞬态回退，故跳过。
        }

        TasksChanged?.Invoke();
    }

    /// <summary>
    /// Dequeues the next valid pending task, skipping already-cancelled entries.
    /// Must be called under lock.
    /// </summary>
    private EncodeTask? DequeueNextValid()
    {
        while (_pendingQueue.Count > 0)
        {
            var next = _pendingQueue.Dequeue();
            if (next.Status == EncodeTaskStatus.Queued)
            {
                return next;
            }
            // Task was cancelled while pending — skip it
        }

        return null;
    }

    private static void AppendLog(EncodeTask task, string message)
    {
        task.LogText += $"[{DateTime.Now:HH:mm:ss}] {message}" + Environment.NewLine;
    }
}
