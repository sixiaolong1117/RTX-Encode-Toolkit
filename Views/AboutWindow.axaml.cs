using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace RTX_Encode_Toolkit.Views;

public partial class AboutWindow : Window
{
    private const string AvatarUrl = "https://avatars.githubusercontent.com/u/59590732";
    private const string RepositoryUrl = "https://github.com/sixiaolong1117/RTX-Encode-Toolkit";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public AboutWindow()
    {
        InitializeComponent();
        LoadAvatarAsync();
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RTX-Encode-Toolkit", "1.0"));
        return httpClient;
    }

    private async void LoadAvatarAsync()
    {
        try
        {
            var bytes = await HttpClient.GetByteArrayAsync(AvatarUrl);
            await using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            await Dispatcher.UIThread.InvokeAsync(() => AvatarImage.Source = bitmap);
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() => AvatarImage.Source = null);
        }
    }

    private void OpenRepository_Click(object? sender, RoutedEventArgs e)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = RepositoryUrl,
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(startInfo);
    }
}
