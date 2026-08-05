using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RTX_Encode_Toolkit.Models;
using RTX_Encode_Toolkit.Services;

namespace RTX_Encode_Toolkit.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly Localization _localization = Localization.Instance;
    private readonly ProcessRunner _processRunner = new();
    private readonly NvencCommandBuilder _commandBuilder;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _nvencToolStatus = string.Empty;

    public EncodeSettingsEditorViewModel Editor { get; }
    public EncodeQueueViewModel Queue { get; }
    public event Action? TaskAdded;

    private bool IsQueueBusy => Queue.IsBusy;

    public bool IsBusy => IsQueueBusy;
    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    public MainWindowViewModel()
    {
        var videoProbeService = new VideoProbeService(_processRunner);
        _commandBuilder = new NvencCommandBuilder(videoProbeService);

        var runner = new EncodeTaskRunner(_processRunner, _commandBuilder);
        var queueService = new EncodeQueueService(runner);
        Editor = new EncodeSettingsEditorViewModel(_commandBuilder);
        Queue = new EncodeQueueViewModel(queueService);
        Queue.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(Queue.IsBusy))
            {
                OnPropertyChanged(nameof(IsBusy));
                CancelAllCommand.NotifyCanExecuteChanged();
            }
        };

        // Listen for editor changes that affect CanAddTask
        Editor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(Editor.InputPath))
            {
                AddTaskCommand.NotifyCanExecuteChanged();
            }

            if (args.PropertyName is not nameof(Editor.CommandPreview))
            {
                Editor.RefreshCommandPreview();
            }
        };

        UpdateNvencStatus();
    }

    internal void UpdateStatusTexts()
    {
        StatusText = string.Empty;
        UpdateNvencStatus();
    }

    partial void OnStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatusText));
    }

    private void UpdateNvencStatus()
    {
        // 只使用内置工具（AppContext.BaseDirectory/tools/）
        var bundledPath = System.IO.Path.Combine(AppContext.BaseDirectory, "tools", "NVEncC64.exe");
        NvencToolStatus = System.IO.File.Exists(bundledPath)
            ? _localization["NvencFound"] + bundledPath
            : _localization["NvencNotFound"];
    }

    public void SetInputPaths(IEnumerable<string> paths)
    {
        Editor.SetInputPaths(paths);
        AddTaskCommand.NotifyCanExecuteChanged();
        UpdateCommandPreview();
    }

    public void SetOutputDirectory(string path)
    {
        Editor.OutputDirectory = path;
        UpdateCommandPreview();
    }

    private void UpdateCommandPreview()
    {
        Editor.RefreshCommandPreview();
    }

    [RelayCommand(CanExecute = nameof(CanAddTask))]
    private void AddTask()
    {
        if (!Editor.HasInputPaths)
        {
            return;
        }

        foreach (var inputPath in Editor.InputPaths)
        {
            var settings = Editor.CreateSnapshot(inputPath);
            var name = System.IO.Path.GetFileNameWithoutExtension(settings.InputPath);
            var task = new EncodeTask { Settings = settings, Name = name };
            Queue.Enqueue(task);
        }

        TaskAdded?.Invoke();
    }

    private bool CanAddTask()
    {
        return Editor.HasInputPaths;
    }

    [RelayCommand(CanExecute = nameof(CanCancelAll))]
    private void CancelAll()
    {
        Queue.CancelAllTasks();
    }

    private bool CanCancelAll()
    {
        return IsQueueBusy;
    }
}
