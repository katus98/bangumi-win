using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace Bangumi.Win.Views;

public sealed partial class PersonDetailPage : Page
{
    private PersonDetail? _person;

    public PersonDetailPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is int personId)
        {
            await LoadPersonAsync(personId);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
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

    private async System.Threading.Tasks.Task LoadPersonAsync(int personId)
    {
        try
        {
            ShowStatus("正在加载人物详情...", InfoBarSeverity.Informational);
            _person = await AppServices.ApiClient.GetPersonAsync(personId);
            NameText.Text = _person.Name;
            CareerText.Text = _person.CareerText;
            SummaryText.Text = _person.Summary ?? _person.ShortSummary ?? "暂无简介";
            if (!string.IsNullOrWhiteSpace(_person.ImageUrl))
            {
                PortraitImage.Source = new BitmapImage(new Uri(_person.ImageUrl));
            }

            HideStatus();
        }
        catch (Exception ex)
        {
            ShowStatus($"加载失败：{ex.Message}", InfoBarSeverity.Error);
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
