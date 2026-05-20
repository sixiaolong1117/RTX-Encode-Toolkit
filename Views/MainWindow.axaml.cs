using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using RTX_Encode_Toolkit.Services;
using RTX_Encode_Toolkit.ViewModels;

namespace RTX_Encode_Toolkit.Views;

public partial class MainWindow : Window
{
    private readonly Localization _localization = Localization.Instance;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void BrowseInput_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = _localization["SelectInputVideo"],
                AllowMultiple = false,
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
            viewModel.SetInputPath(files[0].Path.LocalPath);
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
