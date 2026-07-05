using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;

namespace Bangumi.Win.Views;

public sealed partial class HomePage : Page
{
    private readonly ObservableCollection<TimelineEntry> _timeline = [];
    private ScrollViewer? _timelineScrollViewer;
    private string? _username;
    private int _page = 1;
    private bool _hasMore = true;
    private bool _isLoading;

    public HomePage()
    {
        InitializeComponent();
        TimelineList.ItemsSource = _timeline;
        Loaded += HomePage_Loaded;
    }

    private async void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadTimelineAsync(reset: true);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadTimelineAsync(reset: true);
    }

    private void TimelineList_Loaded(object sender, RoutedEventArgs e)
    {
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
        if (e.ClickedItem is TimelineEntry { SubjectId: int subjectId })
        {
            Frame.Navigate(typeof(SubjectDetailPage), subjectId);
        }
    }

    private async System.Threading.Tasks.Task LoadTimelineAsync(bool reset)
    {
        if (_isLoading)
        {
            return;
        }

        if (!AppServices.TokenStore.HasToken)
        {
            ShowStatus("请先登录后再查看时间胶囊。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            _isLoading = true;
            if (reset)
            {
                _page = 1;
                _hasMore = true;
                _timeline.Clear();
            }

            ShowStatus("正在加载时间胶囊...", InfoBarSeverity.Informational);
            if (_username is null || reset)
            {
                var me = await AppServices.ApiClient.GetMeAsync();
                _username = me.Username;
                SubtitleText.Text = $"{me.Nickname} 的时间胶囊";
            }

            var pageItems = await AppServices.ApiClient.GetTimelineAsync(_username, _page);
            foreach (var item in pageItems)
            {
                _timeline.Add(item);
            }

            _hasMore = pageItems.Count > 0;
            _page++;
            StatusBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            ShowStatus($"加载失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
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
