using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace Bangumi.Win.Views;

public sealed partial class HomePage : Page
{
    private readonly ObservableCollection<TimelineEntry> _timeline = [];
    private ScrollViewer? _timelineScrollViewer;
    private string? _username;
    private int _page = 1;
    private bool _hasMore = true;
    private bool _isLoading;
    private CancellationTokenSource? _loadCts;
    private bool _isSubscribedToAuthChanges;
    private bool _isSubscribedToContentChanges;

    public HomePage()
    {
        InitializeComponent();
        TimelineList.ItemsSource = _timeline;
        Loaded += HomePage_Loaded;
        Unloaded += HomePage_Unloaded;
    }

    private async void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_isSubscribedToAuthChanges)
        {
            AppServices.AuthStateChanged += OnAuthStateChanged;
            _isSubscribedToAuthChanges = true;
        }

        if (!_isSubscribedToContentChanges)
        {
            AppServices.Settings.ContentFilterChanged += OnContentFilterChanged;
            _isSubscribedToContentChanges = true;
        }

        await LoadTimelineAsync(reset: true);
    }

    private void HomePage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_isSubscribedToAuthChanges)
        {
            AppServices.AuthStateChanged -= OnAuthStateChanged;
            _isSubscribedToAuthChanges = false;
        }

        if (_isSubscribedToContentChanges)
        {
            AppServices.Settings.ContentFilterChanged -= OnContentFilterChanged;
            _isSubscribedToContentChanges = false;
        }

        if (_timelineScrollViewer is not null)
        {
            _timelineScrollViewer.ViewChanged -= TimelineScrollViewer_ViewChanged;
            _timelineScrollViewer = null;
        }

        CancelCurrentLoad();
    }

    private async void OnAuthStateChanged(object? sender, EventArgs e)
    {
        _username = null;
        await LoadTimelineAsync(reset: true);
    }

    private async void OnContentFilterChanged(object? sender, EventArgs e)
    {
        await LoadTimelineAsync(reset: true);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadTimelineAsync(reset: true);
    }

    private void TimelineList_Loaded(object sender, RoutedEventArgs e)
    {
        if (_timelineScrollViewer is not null)
        {
            _timelineScrollViewer.ViewChanged -= TimelineScrollViewer_ViewChanged;
        }

        _timelineScrollViewer = FindDescendant<ScrollViewer>(TimelineList);
        if (_timelineScrollViewer is not null)
        {
            _timelineScrollViewer.ViewChanged += TimelineScrollViewer_ViewChanged;
        }
    }

    private async void TimelineScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_timelineScrollViewer is null || !_hasMore || _isLoading)
        {
            return;
        }

        if (_timelineScrollViewer.VerticalOffset >= _timelineScrollViewer.ScrollableHeight - 240)
        {
            await LoadTimelineAsync(reset: false);
        }
    }

    private void TimelineList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TimelineEntry { SubjectId: not null } entry)
        {
            Frame.Navigate(typeof(SubjectDetailPage), entry);
        }
    }

    private async void TimelineReport_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TimelineEntry entry })
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "举报并隐藏这条动态",
            Content = "该动态会立即从本机隐藏。你可以向番喵开发者举报，或前往 Bangumi 网站使用官方处理渠道。",
            PrimaryButtonText = "举报给开发者",
            SecondaryButtonText = "在 Bangumi 打开",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None)
        {
            return;
        }

        AppServices.ContentSafety.HideTimelineEntry(entry);
        _timeline.Remove(entry);

        if (result == ContentDialogResult.Primary)
        {
            var reportUri = AppServices.ContentSafety.CreateReportUri(
                "时间胶囊动态",
                entry.SubjectId ?? 0,
                ContentSafetyService.GetTimelinePublicId(entry));
            await Windows.System.Launcher.LaunchUriAsync(reportUri);
            return;
        }

        var sourceUri = entry.SubjectId is int subjectId
            ? ContentSafetyService.CreateBangumiSubjectUri(subjectId)
            : new Uri($"https://bgm.tv/user/{Uri.EscapeDataString(entry.UserName)}/timeline");
        await Windows.System.Launcher.LaunchUriAsync(sourceUri);
    }

    private async Task LoadTimelineAsync(bool reset)
    {
        if (!reset && _isLoading)
        {
            return;
        }

        var requestCts = reset ? ReplaceLoadCancellation() : _loadCts ??= new CancellationTokenSource();

        if (reset)
        {
            _page = 1;
            _hasMore = true;
            _timeline.Clear();
        }

        if (!AppServices.TokenStore.HasToken)
        {
            SubtitleText.Text = "按时间倒序浏览你的 Bangumi 动态";
            ShowStatus("请先登录后再查看时间胶囊。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            _isLoading = true;
            ShowStatus("正在加载时间胶囊...", InfoBarSeverity.Informational);
            if (_username is null || reset)
            {
                var me = await AppServices.GetCurrentUserAsync(cancellationToken: requestCts.Token);
                _username = me.Username;
                SubtitleText.Text = $"{me.Nickname} 的时间胶囊";
            }

            var addedCount = 0;
            var hiddenCount = 0;
            var fetchedPages = 0;
            do
            {
                var pageItems = await AppServices.ApiClient.GetTimelineAsync(_username, _page, requestCts.Token);
                if (!ReferenceEquals(requestCts, _loadCts))
                {
                    return;
                }

                var visibleItems = await AppServices.ContentSafety.FilterTimelineAsync(pageItems, requestCts.Token);
                if (!ReferenceEquals(requestCts, _loadCts))
                {
                    return;
                }

                foreach (var item in visibleItems)
                {
                    _timeline.Add(item);
                    addedCount++;
                }

                hiddenCount += pageItems.Count - visibleItems.Count;
                _hasMore = pageItems.Count > 0;
                _page++;
                fetchedPages++;
            }
            while (_hasMore && addedCount < 20 && fetchedPages < 3);

            if (hiddenCount > 0)
            {
                ShowStatus($"已按内容安全设置或本地隐藏记录隐藏 {hiddenCount} 条动态。", InfoBarSeverity.Informational);
            }
            else
            {
                HideStatus();
            }
        }
        catch (OperationCanceledException) when (requestCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ShowStatus($"加载失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            if (ReferenceEquals(requestCts, _loadCts))
            {
                _isLoading = false;
            }
        }
    }

    private CancellationTokenSource ReplaceLoadCancellation()
    {
        CancelCurrentLoad();
        _loadCts = new CancellationTokenSource();
        return _loadCts;
    }

    private void CancelCurrentLoad()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        _isLoading = false;
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBarHelper.Show(StatusBar, message, severity);
    }

    private void HideStatus()
    {
        StatusInfoBarHelper.Hide(StatusBar);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
