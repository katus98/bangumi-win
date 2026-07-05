using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Bangumi.Win.Views;

public sealed partial class LoginPage : Page
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        var token = TokenBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            ShowStatus("请输入 access token。", InfoBarSeverity.Warning);
            return;
        }

        AppServices.TokenStore.AccessToken = token;
        await VerifyCurrentTokenAsync();
    }

    private async void Verify_Click(object sender, RoutedEventArgs e)
    {
        await VerifyCurrentTokenAsync();
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        AppServices.TokenStore.Clear();
        AppServices.NotifyAuthStateChanged();
        TokenBox.Password = string.Empty;
        ShowStatus("已退出登录，本地 token 已清除。", InfoBarSeverity.Success);
    }

    private async System.Threading.Tasks.Task VerifyCurrentTokenAsync()
    {
        if (!AppServices.TokenStore.HasToken)
        {
            ShowStatus("当前没有保存 token。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            var me = await AppServices.ApiClient.GetMeAsync();
            AppServices.NotifyAuthStateChanged();
            ShowStatus($"登录成功：{me.Nickname} (@{me.Username})", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            AppServices.TokenStore.Clear();
            AppServices.NotifyAuthStateChanged();
            ShowStatus($"验证失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }
}
