using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace Bangumi.Win.Views;

public sealed partial class SubjectDetailPage : Page
{
    private SubjectSummary? _subject;

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

    private async void AddCollection_Click(object sender, RoutedEventArgs e)
    {
        if (_subject is null)
        {
            return;
        }

        if (!AppServices.TokenStore.HasToken)
        {
            ShowStatus("请先登录后再加入收藏。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            await AppServices.ApiClient.AddCollectionAsync(_subject.Id);
            ShowStatus("已加入在看/在读/在玩。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"加入收藏失败：{ex.Message}", InfoBarSeverity.Error);
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
            if (!string.IsNullOrWhiteSpace(_subject.ImageUrl))
            {
                CoverImage.Source = new BitmapImage(new Uri(_subject.ImageUrl));
            }

            StatusBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            ShowStatus($"加载失败：{ex.Message}", InfoBarSeverity.Error);
        }
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
