using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;

namespace Bangumi.Win.Views;

public sealed partial class EpisodeProgressPage : Page
{
    private SubjectSummary? _subject;

    public EpisodeProgressPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SubjectSummary subject)
        {
            _subject = subject;
            TitleText.Text = subject.DisplayName;
            await LoadEpisodesAsync();
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadEpisodesAsync();

    private async void MarkDone_Click(object sender, RoutedEventArgs e) => await UpdateSelectedAsync(2);

    private async void MarkWish_Click(object sender, RoutedEventArgs e) => await UpdateSelectedAsync(1);

    private async void MarkDropped_Click(object sender, RoutedEventArgs e) => await UpdateSelectedAsync(3);

    private async void MarkNone_Click(object sender, RoutedEventArgs e) => await UpdateSelectedAsync(0);

    private async System.Threading.Tasks.Task LoadEpisodesAsync()
    {
        if (_subject is null)
        {
            return;
        }

        try
        {
            ShowStatus("正在加载单集进度...", InfoBarSeverity.Informational);
            var result = await AppServices.ApiClient.GetEpisodeCollectionsAsync(_subject.Id);
            EpisodeList.ItemsSource = result.Data.OrderBy(item => item.Episode.Sort).ToList();
            HideStatus();
        }
        catch (Exception ex)
        {
            ShowStatus($"加载失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async System.Threading.Tasks.Task UpdateSelectedAsync(int status)
    {
        if (_subject is null)
        {
            return;
        }

        var episodeIds = EpisodeList.SelectedItems
            .OfType<UserEpisodeCollection>()
            .Select(item => item.Episode.Id)
            .ToList();

        if (episodeIds.Count == 0)
        {
            ShowStatus("请先选择要更新的章节。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            await AppServices.ApiClient.UpdateEpisodeCollectionsAsync(_subject.Id, episodeIds, status);
            await LoadEpisodesAsync();
            ShowStatus("单集进度已更新。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"更新失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusPopupHelper.Show(StatusPopup, StatusBar, message, severity, XamlRoot);
    }

    private void HideStatus()
    {
        StatusPopupHelper.Hide(StatusPopup, StatusBar);
    }
}
