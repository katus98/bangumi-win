using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;

namespace Bangumi.Win.Views;

internal static class StatusPopupHelper
{
    private const double Margin = 28;
    private const double FallbackWidth = 360;
    private const double FallbackHeight = 56;

    public static void Show(Popup popup, InfoBar infoBar, string message, InfoBarSeverity severity, XamlRoot? xamlRoot)
    {
        Show(popup, infoBar, message, severity, xamlRoot, retryWhenRootMissing: true);
    }

    private static void Show(Popup popup, InfoBar infoBar, string message, InfoBarSeverity severity, XamlRoot? xamlRoot, bool retryWhenRootMissing)
    {
        try
        {
            var root = xamlRoot ?? popup.XamlRoot ?? infoBar.XamlRoot;
            if (root is null)
            {
                if (retryWhenRootMissing)
                {
                    _ = popup.DispatcherQueue.TryEnqueue(() => Show(popup, infoBar, message, severity, null, retryWhenRootMissing: false));
                }

                return;
            }

            popup.XamlRoot = root;
            infoBar.Message = message;
            infoBar.Severity = severity;
            infoBar.IsOpen = true;
            popup.IsOpen = true;
            infoBar.UpdateLayout();
            Position(popup, infoBar, root);
        }
        catch (Exception)
        {
            infoBar.Message = message;
            infoBar.Severity = severity;
            infoBar.IsOpen = true;
        }
    }

    public static void Hide(Popup popup, InfoBar infoBar)
    {
        infoBar.IsOpen = false;
        popup.IsOpen = false;
    }

    private static void Position(Popup popup, FrameworkElement content, XamlRoot? xamlRoot)
    {
        var width = content.ActualWidth > 0 ? content.ActualWidth : FallbackWidth;
        var height = content.ActualHeight > 0 ? content.ActualHeight : FallbackHeight;
        var size = xamlRoot?.Size;
        popup.HorizontalOffset = Math.Max(Margin, (size?.Width ?? width + Margin * 2) - width - Margin);
        popup.VerticalOffset = Math.Max(Margin, (size?.Height ?? height + Margin * 2) - height - Margin);
    }
}
