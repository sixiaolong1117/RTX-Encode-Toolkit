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

    public ObservableCollection<EncodeTaskViewModel> Tasks { get; } = new();

    [ObservableProperty]
    private EncodeTaskViewModel? _selectedTask;

    public bool IsBusy => Tasks.Any(t => t.IsActive);

    public EncodeQueueViewModel(EncodeQueueService queueService)
    {
        _queueService = queueService;
        _queueService.TasksChanged += OnTasksChanged;
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

        // 增删同步；字段更新由 EncodeTask INPC → EncodeTaskViewModel 转发，无需手工复制
        var serviceTasks = _queueService.GetTasksSnapshot();

        var toRemove = Tasks.Where(vm => !serviceTasks.Any(st => st.Id == vm.Id)).ToList();
        foreach (var vm in toRemove)
        {
            Tasks.Remove(vm);
        }

        EncodeTaskViewModel? firstAdded = null;
        foreach (var task in serviceTasks)
        {
            if (Tasks.All(vm => vm.Id != task.Id))
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

        if (SelectedTask is not null && Tasks.All(task => task.Id != SelectedTask.Id))
        {
            SelectedTask = Tasks.FirstOrDefault();
        }

        OnPropertyChanged(nameof(IsBusy));
    }
}
