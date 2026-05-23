using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RTX_Encode_Toolkit.ViewModels;

namespace RTX_Encode_Toolkit.Views;

public partial class MainWindow : Window
{
    private AddTaskWindow? _addTaskWindow;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private bool _missingToolsWarningShown;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Dispatcher.UIThread.Post(ShowMissingToolsWarningIfNeeded, DispatcherPriority.Background);
    }

    private async void ShowMissingToolsWarningIfNeeded()
    {
        if (_missingToolsWarningShown || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        _missingToolsWarningShown = true;
        var missingTools = viewModel.GetMissingRequiredTools();
        if (missingTools.Count == 0)
        {
            return;
        }

        var dialog = new MissingToolsWindow(missingTools);

        await dialog.ShowDialog(this);
        if (dialog.OpenSettingsRequested)
        {
            await ShowSettingsDialogAsync(viewModel);
        }
    }

    private async void OpenAddTask_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (_addTaskWindow is { IsVisible: true })
        {
            _addTaskWindow.Activate();
            return;
        }

        var window = new AddTaskWindow
        {
            DataContext = viewModel
        };

        _addTaskWindow = window;
        await window.ShowDialog(this);
        if (_addTaskWindow == window)
        {
            _addTaskWindow = null;
        }
    }

    private async void OpenSettings_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await ShowSettingsDialogAsync(viewModel);
        }
    }

    private async Task ShowSettingsDialogAsync(MainWindowViewModel viewModel)
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        var window = new SettingsWindow
        {
            DataContext = new SettingsViewModel(viewModel)
        };

        _settingsWindow = window;
        try
        {
            await window.ShowDialog(this);
        }
        finally
        {
            if (_settingsWindow == window)
            {
                _settingsWindow = null;
            }
        }
    }

    private async void OpenAbout_Click(object? sender, RoutedEventArgs e)
    {
        await ShowAboutDialogAsync();
    }

    private async Task ShowAboutDialogAsync()
    {
        if (_aboutWindow is { IsVisible: true })
        {
            _aboutWindow.Activate();
            return;
        }

        var window = new AboutWindow();

        _aboutWindow = window;
        try
        {
            await window.ShowDialog(this);
        }
        finally
        {
            if (_aboutWindow == window)
            {
                _aboutWindow = null;
            }
        }
    }

    private void LogTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (FollowLogCheckBox.IsChecked != true)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                LogScrollViewer.Offset = LogScrollViewer.Offset.WithY(LogScrollViewer.Extent.Height);
                LogTextBox.CaretIndex = LogTextBox.Text?.Length ?? 0;
            },
            DispatcherPriority.Background);
    }
}
