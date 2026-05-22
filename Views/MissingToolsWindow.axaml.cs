using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RTX_Encode_Toolkit.Models;
using RTX_Encode_Toolkit.Services;

namespace RTX_Encode_Toolkit.Views;

public partial class MissingToolsWindow : Window
{
    private readonly Localization _localization = Localization.Instance;

    public bool OpenSettingsRequested { get; private set; }

    public MissingToolsWindow()
    {
        InitializeComponent();
    }

    public MissingToolsWindow(IReadOnlyList<ToolPathInfo> missingTools)
    {
        InitializeComponent();
        MissingToolsTextBlock.Text = string.Join(Environment.NewLine, missingTools.Select(FormatMissingTool));
    }

    private string FormatMissingTool(ToolPathInfo tool)
    {
        var configuredPath = string.IsNullOrWhiteSpace(tool.ConfiguredPath)
            ? _localization["MissingToolPathUnset"]
            : tool.ConfiguredPath;

        return $"- {tool.DisplayName}: {configuredPath}";
    }

    private void OpenSettings_Click(object? sender, RoutedEventArgs e)
    {
        OpenSettingsRequested = true;
        Close();
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
