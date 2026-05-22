using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using RTX_Encode_Toolkit.ViewModels;
using RTX_Encode_Toolkit.Views;

namespace RTX_Encode_Toolkit;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var showMissingToolsWindowForTesting = desktop.Args?.Any(IsMissingToolsWindowTestArgument) == true;
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(showMissingToolsWindowForTesting),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static bool IsMissingToolsWindowTestArgument(string argument)
    {
        // 测试工具缺失弹窗
        return string.Equals(argument, "--test-missing-tools-window", System.StringComparison.OrdinalIgnoreCase);
    }
}
