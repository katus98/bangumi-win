using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;

namespace Bangumi.Win.Views;

public sealed partial class LoginPage : Page
{
    public LoginPage()
    {
        InitializeComponent();
        Loaded += LoginPage_Loaded;
    }

    private async void LoginPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadCurrentUserAsync(showWarning: false);
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
        ClearUserInfo();
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
            ShowUserInfo(me);
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

    private async System.Threading.Tasks.Task LoadCurrentUserAsync(bool showWarning)
    {
        if (!AppServices.TokenStore.HasToken)
        {
            ClearUserInfo();
            if (showWarning)
            {
                ShowStatus("当前没有保存 token。", InfoBarSeverity.Warning);
            }

            return;
        }

        try
        {
            ShowUserInfo(await AppServices.ApiClient.GetMeAsync());
        }
        catch (Exception ex)
        {
            ClearUserInfo();
            if (showWarning)
            {
                ShowStatus($"读取登录状态失败：{ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private void ShowUserInfo(Models.BangumiUser user)
    {
        LoginStateText.Text = "已登录";
        NicknameText.Text = user.Nickname;
        UserMetaText.Text = $"@{user.Username} · UID {user.Id}";
        var avatar = user.Avatar?.Large ?? user.Avatar?.Medium ?? user.Avatar?.Small;
        AvatarImage.Source = string.IsNullOrWhiteSpace(avatar) ? null : new BitmapImage(new Uri(avatar));
    }

    private void ClearUserInfo()
    {
        LoginStateText.Text = "未登录";
        NicknameText.Text = "尚未连接 Bangumi 账号";
        UserMetaText.Text = string.Empty;
        AvatarImage.Source = null;
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }
}
