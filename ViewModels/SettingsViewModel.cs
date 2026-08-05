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

    public SettingsViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;

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

    /// <summary>
    /// Raised when the window should close after saving.
    /// </summary>
    public event Action? RequestClose;

    private void ApplySettings()
    {
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
