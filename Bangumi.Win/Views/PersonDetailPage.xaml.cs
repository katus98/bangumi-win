using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bangumi.Win.Views;

public sealed partial class PersonDetailPage : Page
{
    private PersonDetail? _person;
    private CancellationTokenSource? _loadCts;

    public PersonDetailPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is int personId)
        {
            var requestCts = ReplaceLoadCancellation();
            await LoadPersonAsync(personId, requestCts.Token);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        CancelCurrentLoad();
        base.OnNavigatedFrom(e);
    }

    private async void Collect_Click(object sender, RoutedEventArgs e)
    {
        if (_person is null)
        {
            return;
        }

        if (!AppServices.TokenStore.HasToken)
        {
            ShowStatus("请先登录后再收藏人物。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            await AppServices.ApiClient.CollectPersonAsync(_person.Id);
            ShowStatus("人物已收藏。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"收藏失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async Task LoadPersonAsync(int personId, CancellationToken cancellationToken)
    {
        try
        {
            ShowStatus("正在加载人物详情...", InfoBarSeverity.Informational);
            _person = await AppServices.ApiClient.GetPersonAsync(personId, cancellationToken);
            NameText.Text = _person.Name;
            CareerText.Text = _person.CareerText;
            SummaryText.Text = _person.Summary ?? _person.ShortSummary ?? "暂无简介";
            if (Uri.TryCreate(_person.ImageUrl, UriKind.Absolute, out var imageUri))
            {
                PortraitImage.Source = new BitmapImage(imageUri);
            }

            HideStatus();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ShowStatus($"加载失败：{ex.Message}", InfoBarSeverity.Error);
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
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBarHelper.Show(StatusBar, message, severity);
    }

    private void HideStatus()
    {
        StatusInfoBarHelper.Hide(StatusBar);
    }
}
