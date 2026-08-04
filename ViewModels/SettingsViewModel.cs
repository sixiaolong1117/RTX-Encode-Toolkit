using System;
using System.Collections.Generic;
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
        (_nvencPathHint, _nvencPathValid, _nvencPathHintColor) = GetPathState(_nvencPath);
        (_ffprobePathHint, _ffprobePathValid, _ffprobePathHintColor) = GetPathState(_ffprobePath);
        (_ffmpegPathHint, _ffmpegPathValid, _ffmpegPathHintColor) = GetPathState(_ffmpegPath);

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

    partial void OnNvencPathChanged(string value)
    {
        (NvencPathHint, NvencPathValid, NvencPathHintColor) = GetPathState(value);
    }

    partial void OnFfprobePathChanged(string value)
    {
        (FfprobePathHint, FfprobePathValid, FfprobePathHintColor) = GetPathState(value);
    }

    partial void OnFfmpegPathChanged(string value)
    {
        (FfmpegPathHint, FfmpegPathValid, FfmpegPathHintColor) = GetPathState(value);
    }

    private static (string Hint, bool Valid, string Color) GetPathState(string value)
    {
        var hint = ToolPathResolver.GetPathHint(value);
        var valid = ToolPathResolver.IsPathValid(value);
        return (hint, valid, valid ? "Gray" : "Red");
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
