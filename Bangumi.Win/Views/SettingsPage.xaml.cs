using Bangumi.Win.Services;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Reflection;
using Windows.ApplicationModel;

namespace Bangumi.Win.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        ThemeSelector.SelectedIndex = (int)AppServices.Settings.Theme;
        ThemeSelector.SelectionChanged += ThemeSelector_SelectionChanged;
        VersionText.Text = $"版本 {GetVersion()}";
    }

    private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeSelector.SelectedIndex is >= 0 and <= 2)
        {
            AppServices.Settings.Theme = (AppTheme)ThemeSelector.SelectedIndex;
        }
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
