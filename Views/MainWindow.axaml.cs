using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RTX_Encode_Toolkit.ViewModels;

namespace RTX_Encode_Toolkit.Views;

public partial class MainWindow : Window
{
    private AddTaskWindow? _addTaskWindow;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OpenAddTask_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (_addTaskWindow is { IsVisible: true })
        {
            _addTaskWindow.Activate();
            return;
        }

        var window = new AddTaskWindow
        {
            DataContext = viewModel
        };

        _addTaskWindow = window;
        await window.ShowDialog(this);
        if (_addTaskWindow == window)
        {
            _addTaskWindow = null;
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
