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
        ApplyTheme();
        ConfigureTitleBar();
        ContentFrame.Navigated += (_, _) => UpdateBackButton();
        AppServices.AuthStateChanged += OnAuthStateChanged;
        AppServices.Settings.ThemeChanged += OnThemeChanged;
        Navigate("home");
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
        var canGoBack = ContentFrame.CanGoBack;
        TitleBackButton.Visibility = canGoBack ? Visibility.Visible : Visibility.Collapsed;
        TitleBackColumn.Width = new GridLength(canGoBack ? 48 : 0);
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
        titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
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

        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            var deferral = args.GetDeferral();
            try
            {
                if (string.IsNullOrWhiteSpace(tokenBox.Password))
                {
                    userInfo.Text = "请输入 access token。";
                    return;
                }

                dialog.IsPrimaryButtonEnabled = false;
                tokenBox.IsEnabled = false;
                userInfo.Text = "正在验证 token...";
                _currentUser = await AppServices.SignInAsync(tokenBox.Password);
                UpdateAccountButton();
                args.Cancel = false;
            }
            catch (Exception ex)
            {
                userInfo.Text = $"登录失败：{ex.Message}";
            }
            finally
            {
                dialog.IsPrimaryButtonEnabled = true;
                tokenBox.IsEnabled = true;
                deferral.Complete();
            }
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary)
        {
            AppServices.SignOut();
        }
    }

    private void OnAuthStateChanged(object? sender, EventArgs e)
    {
        _currentUser = AppServices.CurrentUser;
        UpdateAccountButton();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyTheme();

    private void ApplyTheme()
    {
        RootLayout.RequestedTheme = AppServices.Settings.Theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
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
            _currentUser = await AppServices.GetCurrentUserAsync();
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
