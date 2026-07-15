using Microsoft.UI.Xaml.Controls;

namespace Bangumi.Win.Views;

internal static class StatusInfoBarHelper
{
    public static void Show(InfoBar infoBar, string message, InfoBarSeverity severity)
    {
        infoBar.Message = message;
        infoBar.Severity = severity;
        infoBar.IsOpen = true;
    }

    public static void Hide(InfoBar infoBar)
    {
        infoBar.IsOpen = false;
    }
}
