using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RTX_Encode_Toolkit.Services;

namespace RTX_Encode_Toolkit.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly Localization _localization = Localization.Instance;
    private readonly MainWindowViewModel _mainViewModel;


    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new("System", "SystemLanguage"),
        new("zh-CN", "Chinese"),
        new("en-US", "English"),
    ];

    [ObservableProperty]
    private LanguageOption _selectedLanguage;

    [ObservableProperty]
    private string _nvencPath;

    [ObservableProperty]
    private string _ffprobePath;

    [ObservableProperty]
    private string _ffmpegPath;



    // Path source hints
    [ObservableProperty]
    private string _nvencPathHint = string.Empty;

    [ObservableProperty]
    private string _ffprobePathHint = string.Empty;

    [ObservableProperty]
    private string _ffmpegPathHint = string.Empty;

    // Path validity
    [ObservableProperty]
    private bool _nvencPathValid = true;

    [ObservableProperty]
    private bool _ffprobePathValid = true;

    [ObservableProperty]
    private bool _ffmpegPathValid = true;

    // Path hint foreground color (Red if invalid)
    [ObservableProperty]
    private string _nvencPathHintColor = "Gray";

    [ObservableProperty]
    private string _ffprobePathHintColor = "Gray";

    [ObservableProperty]
    private string _ffmpegPathHintColor = "Gray";


    public SettingsViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;

        // Initialize from main view model
        _nvencPath = mainViewModel.NvencPath;
        _ffprobePath = mainViewModel.FfprobePath;
        _ffmpegPath = mainViewModel.FfmpegPath;

        // Detect path sources and validate (manually set hints since constructor
        // sets backing fields directly, not triggering partial OnXxxChanged methods)
        _nvencPathHint = GetPathHint(_nvencPath, "NVEncC64.exe");
        _nvencPathValid = IsPathValid(_nvencPath);
        _nvencPathHintColor = _nvencPathValid ? "Gray" : "Red";
        _ffprobePathHint = GetPathHint(_ffprobePath, "ffprobe.exe");
        _ffprobePathValid = IsPathValid(_ffprobePath);
        _ffprobePathHintColor = _ffprobePathValid ? "Gray" : "Red";
        _ffmpegPathHint = GetPathHint(_ffmpegPath, "ffmpeg.exe");
        _ffmpegPathValid = IsPathValid(_ffmpegPath);
        _ffmpegPathHintColor = _ffmpegPathValid ? "Gray" : "Red";


        // Set selected language
        var currentCulture = _localization.CurrentCulture;
        _selectedLanguage = LanguageOptions[0]; // System default
        for (int i = 0; i < LanguageOptions.Count; i++)
        {
            if (LanguageOptions[i].Value == currentCulture)
            {
                _selectedLanguage = LanguageOptions[i];
                break;
            }
        }
    }


    private static string GetPathHint(string path, string executableName)
    {
        // Check if it's a full path to an existing file
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            return $"✓ {path}";
        }

        // Check if it's just a name (found via PATH environment variable)
        if (!string.IsNullOrWhiteSpace(path) && !path.Contains(Path.DirectorySeparatorChar) && !path.Contains(Path.AltDirectorySeparatorChar))
        {
            // Try to find it in PATH
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
            foreach (var dir in paths)
            {
                try
                {
                    var fullPath = Path.Combine(dir.Trim(), executableName);
                    if (File.Exists(fullPath))
                    {
                        return $"PATH: {fullPath}";
                    }
                }
                catch
                {
                }
            }
            return "未找到";
        }

        // Check if it's a full path but file doesn't exist
        if (!string.IsNullOrWhiteSpace(path))
        {
            return "文件不存在";
        }

        return "未设置";
    }

    private static bool IsPathValid(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // If it's just a name (like "ffprobe.exe"), check if it exists in PATH
        if (!path.Contains(Path.DirectorySeparatorChar) && !path.Contains(Path.AltDirectorySeparatorChar))
        {
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
            return paths.Any(dir =>
            {
                try
                {
                    return File.Exists(Path.Combine(dir.Trim(), path));
                }
                catch
                {
                    return false;
                }
            });
        }

        return File.Exists(path);
    }

    partial void OnNvencPathChanged(string value)
    {
        NvencPathHint = GetPathHint(value, "NVEncC64.exe");
        NvencPathValid = IsPathValid(value);
        NvencPathHintColor = NvencPathValid ? "Gray" : "Red";
    }

    partial void OnFfprobePathChanged(string value)
    {
        FfprobePathHint = GetPathHint(value, "ffprobe.exe");
        FfprobePathValid = IsPathValid(value);
        FfprobePathHintColor = FfprobePathValid ? "Gray" : "Red";
    }

    partial void OnFfmpegPathChanged(string value)
    {
        FfmpegPathHint = GetPathHint(value, "ffmpeg.exe");
        FfmpegPathValid = IsPathValid(value);
        FfmpegPathHintColor = FfmpegPathValid ? "Gray" : "Red";
    }



    /// <summary>
    /// Raised when the window should close after saving.
    /// </summary>
    public event Action? RequestClose;

    private void ApplySettings()
    {
        // Apply tool paths
        _mainViewModel.NvencPath = NvencPath;
        _mainViewModel.FfprobePath = FfprobePath;
        _mainViewModel.FfmpegPath = FfmpegPath;

        // Apply language
        var langValue = SelectedLanguage.Value;
        if (langValue == "System")
        {
            // Use system culture
            var systemCulture = System.Globalization.CultureInfo.CurrentCulture.Name;
            _localization.CurrentCulture = systemCulture.StartsWith("zh") ? "zh-CN" : "en-US";
        }
        else
        {
            _localization.CurrentCulture = langValue;
        }

        _mainViewModel.UpdateStatusTexts();
    }

    [RelayCommand]
    private void Apply()
    {
        ApplySettings();
    }

    [RelayCommand]
    private void Save()
    {
        ApplySettings();
        RequestClose?.Invoke();
    }

}


public sealed class LanguageOption


{
    public string Value { get; }
    public string DisplayKey { get; }

    public LanguageOption(string value, string displayKey)
    {
        Value = value;
        DisplayKey = displayKey;
    }

    public string GetDisplayName(Localization localization) => localization[DisplayKey];

    public override string ToString() => DisplayKey;
}
