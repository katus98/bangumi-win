using System;
using Windows.Storage;

namespace Bangumi.Win.Services;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public sealed class AppSettings
{
    private const string ThemeKey = "AppTheme";
    private const string ShowNsfwContentKey = "ShowNsfwContent";

    public event EventHandler? ThemeChanged;
    public event EventHandler? ContentFilterChanged;

    public AppTheme Theme
    {
        get
        {
            return ApplicationData.Current.LocalSettings.Values[ThemeKey] is int value
                && Enum.IsDefined(typeof(AppTheme), value)
                    ? (AppTheme)value
                    : AppTheme.System;
        }
        set
        {
            if (Theme == value)
            {
                return;
            }

            ApplicationData.Current.LocalSettings.Values[ThemeKey] = (int)value;
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool ShowNsfwContent
    {
        get => ApplicationData.Current.LocalSettings.Values[ShowNsfwContentKey] is true;
        set
        {
            if (ShowNsfwContent == value)
            {
                return;
            }

            ApplicationData.Current.LocalSettings.Values[ShowNsfwContentKey] = value;
            ContentFilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
