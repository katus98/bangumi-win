using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Bangumi.Win.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using WinRT.Interop;

namespace Bangumi.Win;

public sealed partial class MainWindow : Window
{
    private BangumiUser? _currentUser;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureTitleBar();
        ContentFrame.Navigated += (_, _) => UpdateBackButton();
        AppServices.AuthStateChanged += OnAuthStateChanged;
        Navigate(AppServices.TokenStore.HasToken ? "home" : "home");
        _ = RefreshAccountAsync();
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            Navigate(tag, clearBackStack: true);
        }
    }

    private void Navigate(string tag, bool clearBackStack = false)
    {
        var pageType = tag switch
        {
            "home" => typeof(HomePage),
            "collections" => typeof(CollectionsPage),
            "search" => typeof(SearchPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(HomePage)
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
            if (clearBackStack)
            {
                ContentFrame.BackStack.Clear();
                UpdateBackButton();
            }
        }
    }

    private void TitleBackButton_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
        }
    }

    private void UpdateBackButton()
    {
        TitleBackButton.Visibility = ContentFrame.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ConfigureTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Title = "番喵";

        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = appWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonHoverBackgroundColor = Colors.Transparent;
        titleBar.ButtonPressedBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = Colors.White;
        titleBar.ButtonInactiveForegroundColor = Colors.Gray;
    }

    private async void AccountButton_Click(object sender, RoutedEventArgs e)
    {
        var tokenBox = new PasswordBox
        {
            Header = "Access token",
            PlaceholderText = "粘贴 Bangumi access token",
            PasswordRevealMode = PasswordRevealMode.Peek
        };
        var userInfo = new TextBlock
        {
            Text = _currentUser is null ? "当前未登录" : $"已登录：{_currentUser.Nickname} (@{_currentUser.Username})",
            TextWrapping = TextWrapping.Wrap
        };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(userInfo);
        panel.Children.Add(tokenBox);

        var dialog = new ContentDialog
        {
            Title = "番喵账号",
            Content = panel,
            PrimaryButtonText = "登录/更新",
            SecondaryButtonText = AppServices.TokenStore.HasToken ? "退出登录" : string.Empty,
            CloseButtonText = "关闭",
            XamlRoot = ContentFrame.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(tokenBox.Password))
            {
                return;
            }

            AppServices.TokenStore.AccessToken = tokenBox.Password;
            await RefreshAccountAsync();
            AppServices.NotifyAuthStateChanged();
        }
        else if (result == ContentDialogResult.Secondary)
        {
            AppServices.TokenStore.Clear();
            _currentUser = null;
            UpdateAccountButton();
            AppServices.NotifyAuthStateChanged();
        }
    }

    private async void OnAuthStateChanged(object? sender, EventArgs e)
    {
        await RefreshAccountAsync();
    }

    private async System.Threading.Tasks.Task RefreshAccountAsync()
    {
        if (!AppServices.TokenStore.HasToken)
        {
            _currentUser = null;
            UpdateAccountButton();
            return;
        }

        try
        {
            _currentUser = await AppServices.ApiClient.GetMeAsync();
        }
        catch
        {
            _currentUser = null;
        }

        UpdateAccountButton();
    }

    private void UpdateAccountButton()
    {
        AccountText.Text = _currentUser?.Nickname ?? "登录";
        var avatar = _currentUser?.Avatar?.Medium ?? _currentUser?.Avatar?.Small ?? _currentUser?.Avatar?.Large;
        AccountAvatar.Source = string.IsNullOrWhiteSpace(avatar) ? null : new BitmapImage(new Uri(avatar));
    }
}
