using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
}






