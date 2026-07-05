using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;

namespace Bangumi.Win.Views;

public sealed partial class SubjectDetailPage : Page
{
    private SubjectSummary? _subject;
    private SubjectCollection? _collection;

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

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private async void CollectionStatus_Click(object sender, RoutedEventArgs e)
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

        var statusBox = new ComboBox
        {
            Header = "收藏状态",
            ItemsSource = BangumiConstants.GetEditableCollectionStatuses(_subject.Type),
            DisplayMemberPath = "Name",
            SelectedIndex = Math.Max(0, IndexOfStatus(_collection?.Type ?? 3, _subject.Type))
        };

        var dialog = new ContentDialog
        {
            Title = _subject.DisplayName,
            Content = statusBox,
            PrimaryButtonText = _collection is null ? "加入收藏" : "保存",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            var status = statusBox.SelectedItem is OptionItem<int> selected ? selected.Value : 3;
            await AppServices.ApiClient.UpdateCollectionAsync(_subject.Id, status, null, null);
            await LoadCollectionAsync();
            ShowStatus("收藏状态已更新。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"收藏状态更新失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async System.Threading.Tasks.Task LoadSubjectAsync(int subjectId)
    {
        try
        {
            ShowStatus("正在加载条目详情...", InfoBarSeverity.Informational);
            _subject = await AppServices.ApiClient.GetSubjectAsync(subjectId);
            TitleText.Text = _subject.DisplayName;
            SubtitleText.Text = _subject.Subtitle;
            ScoreText.Text = _subject.Score is double score ? $"评分 {score:0.0}" : "暂无评分";
            ProgressHintText.Text = BuildProgressHint(_subject);
            SummaryText.Text = _subject.Summary ?? "暂无简介";
            CoverImage.Source = string.IsNullOrWhiteSpace(_subject.ImageUrl) ? null : new BitmapImage(new Uri(_subject.ImageUrl));

            await LoadCollectionAsync();
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
        CollectionStatusText.Text = "未收藏，点击此处加入收藏";
        EpisodeStatusList.Visibility = Visibility.Collapsed;
        EpisodeStatusList.ItemsSource = null;

        if (_subject is null || !AppServices.TokenStore.HasToken)
        {
            return;
        }

        try
        {
            var me = await AppServices.ApiClient.GetMeAsync();
            _collection = await AppServices.ApiClient.GetCollectionAsync(me.Username, _subject.Id);
            CollectionStatusText.Text = $"{_collection.StatusLabel} · {_collection.ProgressLabel}";

            if (_subject.Type == 2)
            {
                var episodes = await AppServices.ApiClient.GetEpisodeCollectionsAsync(_subject.Id);
                EpisodeStatusList.ItemsSource = episodes.Data.OrderBy(item => item.Episode.Sort).ToList();
                EpisodeStatusList.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            CollectionStatusText.Text = "未收藏，点击此处加入收藏";
        }
    }

    private async void EpisodeWish_Click(object sender, RoutedEventArgs e) => await UpdateEpisodeAsync(sender, 1);

    private async void EpisodeDone_Click(object sender, RoutedEventArgs e) => await UpdateEpisodeAsync(sender, 2);

    private async void EpisodeDropped_Click(object sender, RoutedEventArgs e) => await UpdateEpisodeAsync(sender, 3);

    private async void EpisodeNone_Click(object sender, RoutedEventArgs e) => await UpdateEpisodeAsync(sender, 0);

    private async System.Threading.Tasks.Task UpdateEpisodeAsync(object sender, int status)
    {
        if (_subject is null || sender is not Button { Tag: UserEpisodeCollection episode })
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
            await AppServices.ApiClient.UpdateEpisodeCollectionsAsync(_subject.Id, [episode.Episode.Id], status);
            await LoadCollectionAsync();
            ShowStatus("单集状态已更新。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"单集状态更新失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private static int IndexOfStatus(int status, int subjectType)
    {
        var statuses = BangumiConstants.GetEditableCollectionStatuses(subjectType);
        for (var i = 0; i < statuses.Count; i++)
        {
            if (statuses[i].Value == status)
            {
                return i;
            }
        }

        return 0;
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
}
