using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly bool _showMissingToolsWindowForTesting;

    // Tool paths (shared across all tasks, synced with SettingsWindow)
    [ObservableProperty]
    private string _nvencPath = "NVEncC64.exe";

    [ObservableProperty]
    private string _ffprobePath = "ffprobe.exe";

    [ObservableProperty]
    private string _ffmpegPath = "ffmpeg.exe";

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
        : this(false)
    {
    }

    public MainWindowViewModel(bool showMissingToolsWindowForTesting)
    {
        _showMissingToolsWindowForTesting = showMissingToolsWindowForTesting;

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

        SyncToolPathsToEditor();

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

        // Listen for tool path changes and sync to editor
        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(NvencPath) or nameof(FfprobePath) or nameof(FfmpegPath))
            {
                SyncToolPathsToEditor();
            }

            UpdateCommandPreview();
        };

        UpdateNvencStatus();
    }

    private void SyncToolPathsToEditor()
    {
        Editor.NvencPath = NvencPath;
        Editor.FfprobePath = FfprobePath;
        Editor.FfmpegPath = FfmpegPath;
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
        // Check if NVEncC64.exe is available via PATH or configured path
        var path = NvencPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            NvencToolStatus = _localization["NvencUsingPath"];
            return;
        }

        var result = ToolPathResolver.Resolve(path);
        if (result.Status == ToolPathLookupStatus.FoundInPath)
        {
            NvencToolStatus = _localization["NvencUsingPath"];
            return;
        }

        if (result.Status == ToolPathLookupStatus.FoundAtPath)
        {
            NvencToolStatus = _localization["NvencFound"] + result.ResolvedPath;
        }
        else
        {
            NvencToolStatus = _localization["NvencNotFound"];
        }
    }

    public IReadOnlyList<ToolPathInfo> GetMissingRequiredTools()
    {
        if (_showMissingToolsWindowForTesting)
        {
            return GetRequiredTools();
        }

        return GetRequiredTools()
            .Where(tool => !ToolPathResolver.IsPathValid(tool.ConfiguredPath))
            .ToList();
    }

    private IReadOnlyList<ToolPathInfo> GetRequiredTools()
    {
        return
        [
            new("NVEncC", NvencPath),
            new("ffprobe", FfprobePath),
            new("ffmpeg", FfmpegPath),
        ];
    }

    public void SetInputPath(string path)
    {
        SetInputPaths([path]);
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

    private Views.SettingsWindow? _settingsWindow;
    private Views.AboutWindow? _aboutWindow;

    [RelayCommand]
    private void OpenSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new Views.SettingsWindow
        {
            DataContext = new SettingsViewModel(this)
        };
        _settingsWindow.Show();
    }

    [RelayCommand]
    private void OpenAbout()
    {
        if (_aboutWindow is { IsVisible: true })
        {
            _aboutWindow.Activate();
            return;
        }

        _aboutWindow = new Views.AboutWindow();
        _aboutWindow.Show();
    }
}
