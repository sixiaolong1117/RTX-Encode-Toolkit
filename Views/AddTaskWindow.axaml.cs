using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RTX_Encode_Toolkit.Services;
using RTX_Encode_Toolkit.ViewModels;

namespace RTX_Encode_Toolkit.Views;

public partial class AddTaskWindow : Window
{
    private readonly Localization _localization = Localization.Instance;
    private MainWindowViewModel? _viewModel;

    public AddTaskWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.TaskAdded -= CloseAfterTaskAdded;
        }

        base.OnDataContextChanged(e);

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.TaskAdded += CloseAfterTaskAdded;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.TaskAdded -= CloseAfterTaskAdded;
            _viewModel = null;
        }

        base.OnClosed(e);
    }

    private void CloseAfterTaskAdded()
    {
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void BrowseInput_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = _localization["SelectInputVideo"],
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType(_localization["VideoFiles"])
                    {
                        Patterns = ["*.mp4", "*.mkv", "*.mov", "*.avi", "*.webm", "*.m2ts", "*.ts"]
                    },
                    FilePickerFileTypes.All
                ]
            });

        if (files.Count > 0 && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetInputPaths(files.Select(file => file.Path.LocalPath));
        }
    }

    private async void BrowseOutput_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = _localization["SelectOutputDir"],
                AllowMultiple = false
            });

        if (folders.Count > 0 && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetOutputDirectory(folders[0].Path.LocalPath);
        }
    }
}
