using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RTX_Encode_Toolkit.ViewModels;

namespace RTX_Encode_Toolkit.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void BrowseInput_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "选择输入视频",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("视频文件")
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
                Title = "选择输出目录",
                AllowMultiple = false
            });

        if (folders.Count > 0 && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetOutputDirectory(folders[0].Path.LocalPath);
        }
    }
}
