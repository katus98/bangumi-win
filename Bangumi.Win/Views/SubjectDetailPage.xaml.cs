using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace Bangumi.Win.Views;

public sealed partial class SubjectDetailPage : Page
{
    private SubjectSummary? _subject;
    private SubjectCollection? _collection;
    private int? _collectionStatus;
    private readonly ObservableCollection<SubjectComment> _comments = [];
    private List<UserEpisodeCollection> _episodes = [];
    private List<EpisodeStatusRow> _episodeRows = [];
    private bool _isLoadingComments;
    private bool _commentsFinished;
    private int _commentOffset;

    public SubjectDetailPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is int subjectId)
        {
            await LoadSubjectAsync(subjectId);
        }
    }

    private void CollectionStatus_Click(object sender, RoutedEventArgs e)
    {
        if (_subject is null)
        {
            return;
        }

        if (!AppServices.TokenStore.HasToken)
        {
            ShowStatus("请先登录后再编辑收藏状态。", InfoBarSeverity.Warning);
            return;
        }

        if (sender is not Button button)
        {
            return;
        }

        var flyout = new MenuFlyout();
        foreach (var status in BangumiConstants.GetEditableCollectionStatuses(_subject.Type))
        {
            var item = new MenuFlyoutItem { Text = status.Name, Tag = status.Value };
            item.Click += async (_, _) => await UpdateCollectionStatusAsync((int)item.Tag);
            flyout.Items.Add(item);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());
        var cancelItem = new MenuFlyoutItem { Text = "取消收藏", IsEnabled = _collectionStatus is not null };
        cancelItem.Click += async (_, _) => await DeleteCollectionAsync();
        flyout.Items.Add(cancelItem);

        flyout.ShowAt(button);
    }

    private async System.Threading.Tasks.Task LoadSubjectAsync(int subjectId)
    {
        try
        {
            ShowStatus("正在加载条目详情...", InfoBarSeverity.Informational);
            _subject = await AppServices.ApiClient.GetSubjectAsync(subjectId);
            TitleText.Text = _subject.DisplayName;
            SubtitleText.Text = _subject.Subtitle;
            TypeBadgeText.Text = _subject.TypeLabel;
            ScoreText.Text = _subject.Score is double score ? $"评分 {score:0.0}" : "暂无评分";
            ProgressHintText.Text = BuildProgressHint(_subject);
            var tags = _subject.Tags is { Count: > 0 } ? _subject.Tags.Take(16).ToList() : [];
            TagsItems.ItemsSource = tags;
            TagsEmptyText.Visibility = tags.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SummaryText.Text = _subject.Summary ?? "暂无简介";
            var cover = string.IsNullOrWhiteSpace(_subject.ImageUrl) ? null : new BitmapImage(new Uri(_subject.ImageUrl));
            CoverImage.Source = cover;
            BackdropImage.Source = cover;
            CommentList.ItemsSource = _comments;
            _comments.Clear();
            _commentOffset = 0;
            _commentsFinished = false;
            CommentStatusText.Text = "正在加载吐槽...";

            await LoadCollectionAsync();
            await LoadCommentsAsync(reset: true);
            StatusBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            ShowStatus($"加载失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async System.Threading.Tasks.Task LoadCollectionAsync()
    {
        _collection = null;
        _collectionStatus = null;
        UpdateCollectionButton(null);
        EpisodeStatusList.Visibility = Visibility.Collapsed;
        EpisodeStatusList.ItemsSource = null;
        EpisodeEmptyText.Visibility = Visibility.Visible;
        _episodes = [];
        _episodeRows = [];

        if (_subject is null || !AppServices.TokenStore.HasToken)
        {
            return;
        }

        try
        {
            var me = await AppServices.ApiClient.GetMeAsync();
            _collection = await AppServices.ApiClient.GetCollectionAsync(me.Username, _subject.Id);
            _collectionStatus = _collection.Type;
            UpdateCollectionButton(_collectionStatus);

            if (_subject.Type == 2)
            {
                var episodes = await AppServices.ApiClient.GetEpisodeCollectionsAsync(_subject.Id);
                _episodes = episodes.Data.OrderBy(item => item.Episode.Sort).ToList();
                _episodeRows = _episodes.Select(item => new EpisodeStatusRow(item)).ToList();
                EpisodeStatusList.ItemsSource = _episodeRows;
                EpisodeStatusList.Visibility = _episodeRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                EpisodeEmptyText.Visibility = _episodeRows.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            }
        }
        catch
        {
            _collectionStatus = null;
            UpdateCollectionButton(null);
        }
    }

    private void EpisodeStatus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not EpisodeStatusRow row)
        {
            return;
        }

        var flyout = new MenuFlyout();
        foreach (var option in new[] { ("取消收藏", 0), ("想看", 1), ("看过", 2), ("抛弃", 3) })
        {
            var item = new MenuFlyoutItem { Text = option.Item1, Tag = option.Item2 };
            item.Click += async (_, _) => await UpdateEpisodeAsync(row.Source, (int)item.Tag, includePrevious: false);
            flyout.Items.Add(item);
        }

        var watchedTo = new MenuFlyoutItem { Text = "看到" };
        watchedTo.Click += async (_, _) => await UpdateEpisodeAsync(row.Source, 2, includePrevious: true);
        flyout.Items.Add(watchedTo);
        flyout.ShowAt(button);
    }

    private async System.Threading.Tasks.Task UpdateCollectionStatusAsync(int status)
    {
        if (_subject is null)
        {
            return;
        }

        try
        {
            await AppServices.ApiClient.UpdateCollectionAsync(_subject.Id, status, null, null);
            _collectionStatus = status;
            UpdateCollectionButton(status);
            ShowStatus("收藏状态已更新。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"收藏状态更新失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async System.Threading.Tasks.Task DeleteCollectionAsync()
    {
        if (_subject is null)
        {
            return;
        }

        try
        {
            await AppServices.ApiClient.DeleteCollectionAsync(_subject.Id);
            _collection = null;
            _collectionStatus = null;
            UpdateCollectionButton(null);
            ShowStatus("已取消收藏。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"取消收藏失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async System.Threading.Tasks.Task UpdateEpisodeAsync(UserEpisodeCollection episode, int status, bool includePrevious)
    {
        if (_subject is null)
        {
            return;
        }

        if (!AppServices.TokenStore.HasToken)
        {
            ShowStatus("请先登录后再修改单集状态。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            var ids = includePrevious
                ? _episodeRows.Where(item => item.Source.Episode.Sort <= episode.Episode.Sort).Select(item => item.Source.Episode.Id).ToList()
                : [episode.Episode.Id];
            await AppServices.ApiClient.UpdateEpisodeCollectionsAsync(_subject.Id, ids, status);
            foreach (var row in _episodeRows.Where(item => ids.Contains(item.Source.Episode.Id)))
            {
                row.UpdateStatus(status);
            }

            ShowStatus("单集状态已更新。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"单集状态更新失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async System.Threading.Tasks.Task LoadCommentsAsync(bool reset = false)
    {
        if (_subject is null || _isLoadingComments || (_commentsFinished && !reset))
        {
            return;
        }

        if (reset)
        {
            _comments.Clear();
            _commentOffset = 0;
            _commentsFinished = false;
        }

        try
        {
            _isLoadingComments = true;
            CommentStatusText.Text = _commentOffset == 0 ? "正在加载吐槽..." : "正在加载更多...";
            var page = await AppServices.ApiClient.GetSubjectCommentsAsync(_subject.Id, _commentOffset);
            foreach (var comment in page.Data)
            {
                _comments.Add(comment);
            }

            _commentOffset += page.Data.Count;
            _commentsFinished = page.Data.Count == 0 || _commentOffset >= page.Total;
            CommentStatusText.Text = _comments.Count == 0
                ? "暂无吐槽"
                : _commentsFinished ? "没有更多吐槽了" : "继续下滑加载更多";
        }
        catch
        {
            _commentsFinished = true;
            CommentStatusText.Text = _comments.Count == 0 ? "暂无吐槽" : "没有更多吐槽了";
        }
        finally
        {
            _isLoadingComments = false;
        }
    }

    private async void DetailScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || e.IsIntermediate)
        {
            return;
        }

        if (scrollViewer.VerticalOffset + scrollViewer.ViewportHeight >= scrollViewer.ExtentHeight - 160)
        {
            await LoadCommentsAsync();
        }
    }

    private void UpdateCollectionButton(int? status)
    {
        if (_subject is null)
        {
            return;
        }

        CollectionStatusButton.Content = status is int value
            ? BangumiConstants.CollectionStatusLabel(_subject.Type, value)
            : "未收藏";
        CollectionStatusButton.Background = StatusBackground(status);
        CollectionStatusButton.Foreground = StatusForeground(status);
    }

    private static SolidColorBrush StatusBackground(int? status)
    {
        return new SolidColorBrush(status switch
        {
            1 => Microsoft.UI.Colors.SteelBlue,
            2 => Microsoft.UI.Colors.SeaGreen,
            3 => Microsoft.UI.Colors.MediumPurple,
            4 => Microsoft.UI.Colors.Gray,
            5 => Microsoft.UI.Colors.IndianRed,
            _ => Microsoft.UI.Colors.LightGray
        });
    }

    private static SolidColorBrush StatusForeground(int? status)
    {
        return new SolidColorBrush(status is null or 0 ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White);
    }

    private static string BuildProgressHint(SubjectSummary subject)
    {
        return subject.Type switch
        {
            1 when subject.Volumes is > 0 => $"全 {subject.Volumes} 卷",
            2 when subject.Eps is > 0 => $"全 {subject.Eps} 话",
            _ when subject.Eps is > 0 => $"全 {subject.Eps} 章节",
            _ => subject.TypeLabel
        };
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }

    private sealed class EpisodeStatusRow(UserEpisodeCollection source) : INotifyPropertyChanged
    {
        private int _type = source.Type;

        public event PropertyChangedEventHandler? PropertyChanged;

        public UserEpisodeCollection Source { get; } = source;
        public EpisodeSummary Episode => Source.Episode;
        public string ActionLabel => _type == 0 ? "未收藏" : _type switch
        {
            1 => "想看",
            2 => "看过",
            3 => "抛弃",
            _ => "未知"
        };

        public SolidColorBrush ActionBackground => StatusBackground(_type);
        public SolidColorBrush ActionForeground => StatusForeground(_type);

        public void UpdateStatus(int status)
        {
            _type = status;
            OnPropertyChanged(nameof(ActionLabel));
            OnPropertyChanged(nameof(ActionBackground));
            OnPropertyChanged(nameof(ActionForeground));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
