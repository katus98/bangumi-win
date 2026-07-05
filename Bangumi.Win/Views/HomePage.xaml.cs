using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Bangumi.Win.Views;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        Loaded += HomePage_Loaded;
    }

    private async void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadTimelineAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadTimelineAsync();
    }

    private async System.Threading.Tasks.Task LoadTimelineAsync()
    {
        if (!AppServices.TokenStore.HasToken)
        {
            ShowStatus("请先登录后再查看时间胶囊。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            ShowStatus("正在加载时间胶囊...", InfoBarSeverity.Informational);
            var me = await AppServices.ApiClient.GetMeAsync();
            SubtitleText.Text = $"{me.Nickname} 的时间胶囊";
            TimelineList.ItemsSource = await AppServices.ApiClient.GetTimelineAsync(me.Username);
            StatusBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            ShowStatus($"加载失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }
}
