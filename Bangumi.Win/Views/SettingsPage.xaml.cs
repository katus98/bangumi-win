using Bangumi.Win.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Reflection;
using Windows.ApplicationModel;

namespace Bangumi.Win.Views;

public sealed partial class SettingsPage : Page
{
    private bool _updatingSensitiveContentToggle;

    public SettingsPage()
    {
        InitializeComponent();
        ThemeSelector.SelectedIndex = (int)AppServices.Settings.Theme;
        ThemeSelector.SelectionChanged += ThemeSelector_SelectionChanged;
        SensitiveContentToggle.IsOn = AppServices.Settings.ShowNsfwContent;
        SensitiveContentToggle.Toggled += SensitiveContentToggle_Toggled;
        AppServices.ContentSafety.HiddenContentChanged += OnHiddenContentChanged;
        Unloaded += SettingsPage_Unloaded;
        UpdateHiddenContentCount();
        VersionText.Text = $"版本 {GetVersion()}";
    }

    private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        AppServices.ContentSafety.HiddenContentChanged -= OnHiddenContentChanged;
    }

    private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeSelector.SelectedIndex is >= 0 and <= 2)
        {
            AppServices.Settings.Theme = (AppTheme)ThemeSelector.SelectedIndex;
        }
    }

    private async void SensitiveContentToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingSensitiveContentToggle)
        {
            return;
        }

        if (!SensitiveContentToggle.IsOn)
        {
            AppServices.Settings.ShowNsfwContent = false;
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "显示成人或敏感内容？",
            Content = "Bangumi 标记为 NSFW 的条目可能包含裸露、性暗示或其他不适合未成年人的内容。继续表示你已了解风险并主动选择显示。",
            PrimaryButtonText = "我已了解，显示",
            CloseButtonText = "保持隐藏",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            AppServices.Settings.ShowNsfwContent = true;
            return;
        }

        _updatingSensitiveContentToggle = true;
        SensitiveContentToggle.IsOn = false;
        _updatingSensitiveContentToggle = false;
    }

    private void ClearHiddenContent_Click(object sender, RoutedEventArgs e)
    {
        AppServices.ContentSafety.ClearHiddenContent();
    }

    private void OnHiddenContentChanged(object? sender, EventArgs e) => UpdateHiddenContentCount();

    private void UpdateHiddenContentCount()
    {
        HiddenContentCountText.Text = $"已隐藏 {AppServices.ContentSafety.HiddenContentCount} 项";
    }

    private static string GetVersion()
    {
        try
        {
            var version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
        catch (InvalidOperationException)
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        }
    }
}
