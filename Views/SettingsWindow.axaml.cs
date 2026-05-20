using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RTX_Encode_Toolkit.Services;
using RTX_Encode_Toolkit.ViewModels;

namespace RTX_Encode_Toolkit.Views;

public partial class SettingsWindow : Window
{
    private readonly Localization _localization = Localization.Instance;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)

    {
        base.OnDataContextChanged(e);
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.RequestClose += () => Close();
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void BrowseNvenc_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Select NVEncC64.exe",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("NVEncC64.exe")
                    {
                        Patterns = ["NVEncC64.exe"]
                    },
                    FilePickerFileTypes.All
                ]
            });

        if (files.Count > 0 && DataContext is SettingsViewModel vm)
        {
            vm.NvencPath = files[0].Path.LocalPath;
        }
    }

    private async void BrowseFfprobe_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Select ffprobe.exe",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("ffprobe.exe")
                    {
                        Patterns = ["ffprobe.exe"]
                    },
                    FilePickerFileTypes.All
                ]
            });

        if (files.Count > 0 && DataContext is SettingsViewModel vm)
        {
            vm.FfprobePath = files[0].Path.LocalPath;
        }
    }

    private async void BrowseFfmpeg_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Select ffmpeg.exe",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("ffmpeg.exe")
                    {
                        Patterns = ["ffmpeg.exe"]
                    },
                    FilePickerFileTypes.All
                ]
            });

        if (files.Count > 0 && DataContext is SettingsViewModel vm)
        {
            vm.FfmpegPath = files[0].Path.LocalPath;
        }
    }

    private void OpenNvencRelease_Click(object? sender, RoutedEventArgs e)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://github.com/rigaya/NVEnc/releases/latest",
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(psi);
    }

    private void OpenFfmpegRelease_Click(object? sender, RoutedEventArgs e)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://www.gyan.dev/ffmpeg/builds/",
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(psi);
    }
}






