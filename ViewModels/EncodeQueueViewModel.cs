using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RTX_Encode_Toolkit.Services;

namespace RTX_Encode_Toolkit.ViewModels;

public partial class EncodeQueueViewModel : ViewModelBase
{
    private readonly EncodeQueueService _queueService;
    private readonly DispatcherTimer _refreshTimer;

    public ObservableCollection<EncodeTaskViewModel> Tasks { get; } = new();

    [ObservableProperty]
    private EncodeTaskViewModel? _selectedTask;

    public bool IsBusy => Tasks.Any(t => t.IsActive);

    public EncodeQueueViewModel(EncodeQueueService queueService)
    {
        _queueService = queueService;

        _queueService.TasksChanged += OnTasksChanged;

        // Periodically refresh running tasks to show live log & status
        _refreshTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(200),
            DispatcherPriority.Background,
            (_, _) => RefreshRunningTasks());
        _refreshTimer.Start();
    }

    public void Enqueue(Models.EncodeTask task)
    {
        _queueService.Enqueue(task);
    }

    public void CancelAllTasks()
    {
        _queueService.CancelAllTasks();
    }

    [RelayCommand]
    private void CancelTask(string? taskId)
    {
        if (taskId is not null)
        {
            _queueService.CancelTask(taskId);
        }
    }

    [RelayCommand]
    private void RemoveTask(string? taskId)
    {
        if (taskId is null)
        {
            return;
        }

        _queueService.RemoveTask(taskId);
    }

    private void OnTasksChanged()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnTasksChanged, DispatcherPriority.Background);
            return;
        }

        // Sync Tasks collection with the service's task list
        var serviceTasks = _queueService.GetTasksSnapshot();

        // Remove view models for tasks no longer in service list
        var toRemove = Tasks.Where(vm => !serviceTasks.Any(st => st.Id == vm.Id)).ToList();
        foreach (var vm in toRemove)
        {
            Tasks.Remove(vm);
        }

        // Add new tasks
        EncodeTaskViewModel? firstAdded = null;
        foreach (var task in serviceTasks)
        {
            if (!Tasks.Any(vm => vm.Id == task.Id))
            {
                var taskViewModel = new EncodeTaskViewModel(task, CancelTask, RemoveTask);
                Tasks.Add(taskViewModel);
                firstAdded ??= taskViewModel;
            }
        }

        if (SelectedTask is null && firstAdded is not null)
        {
            SelectedTask = firstAdded;
        }

        // Update existing view models
        foreach (var vm in Tasks)
        {
            var task = serviceTasks.FirstOrDefault(t => t.Id == vm.Id);
            if (task is null)
            {
                continue;
            }

            SyncTaskViewModel(vm, task);
        }

        if (SelectedTask is not null && Tasks.All(task => task.Id != SelectedTask.Id))
        {
            SelectedTask = Tasks.FirstOrDefault();
        }

        OnPropertyChanged(nameof(IsBusy));
    }

    private void RefreshRunningTasks()
    {
        var serviceTasks = _queueService.GetTasksSnapshot();

        foreach (var vm in Tasks)
        {
            var task = serviceTasks.FirstOrDefault(t => t.Id == vm.Id);
            if (task is null)
            {
                continue;
            }

            // Only refresh running/cancelling tasks (live state)
            if (task.Status is not (Models.EncodeTaskStatus.Running or Models.EncodeTaskStatus.Cancelling))
            {
                continue;
            }

            SyncTaskViewModel(vm, task);
        }

        OnPropertyChanged(nameof(IsBusy));
    }

    private static void SyncTaskViewModel(EncodeTaskViewModel vm, Models.EncodeTask task)
    {
        vm.Status = task.Status;
        vm.StartedAt = task.StartedAt;
        vm.FinishedAt = task.FinishedAt;
        vm.OutputPath = task.OutputPath;
        vm.ErrorMessage = task.ErrorMessage;
        vm.LogText = task.LogText;
        vm.Name = task.Name;
        vm.ProgressText = task.ProgressText;
        vm.ProgressPercent = task.ProgressPercent;

        vm.NotifyPropertyChanged(nameof(vm.StatusText));
        vm.NotifyPropertyChanged(nameof(vm.DurationText));
        vm.NotifyPropertyChanged(nameof(vm.CanCancel));
        vm.NotifyPropertyChanged(nameof(vm.CanRemove));
        vm.NotifyPropertyChanged(nameof(vm.IsActive));
        vm.NotifyPropertyChanged(nameof(vm.HasProgressText));
        vm.NotifyPropertyChanged(nameof(vm.HasProgressPercent));
        vm.NotifyPropertyChanged(nameof(vm.IsProgressIndeterminate));
        vm.NotifyPropertyChanged(nameof(vm.ProgressPercentValue));
        vm.NotifyPropertyChanged(nameof(vm.CanOpenFolder));
        vm.NotifyCommandStateChanged();
    }
}
