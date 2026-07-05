using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;

namespace Bangumi.Win.Views;

public sealed partial class SearchPage : Page
{
    public SearchPage()
    {
        InitializeComponent();
        SearchTypeBox.ItemsSource = BangumiConstants.SearchTypes;
        SearchTypeBox.SelectedIndex = 0;
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        await SearchAsync();
    }

    private async void KeywordBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await SearchAsync();
        }
    }

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SearchResultItem item })
        {
            if (item.IsPerson)
            {
                _ = ShowPersonAsync(item);
            }
            else
            {
                Frame.Navigate(typeof(SubjectDetailPage), item.Id);
            }
        }
    }

    private async void AddCollection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SearchResultItem item })
        {
            return;
        }

        if (item.IsPerson)
        {
            ShowStatus("人物搜索结果不能加入条目收藏。", InfoBarSeverity.Warning);
            return;
        }

        if (!AppServices.TokenStore.HasToken)
        {
            ShowStatus("请先登录后再加入收藏。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            await AppServices.ApiClient.AddCollectionAsync(item.Id);
            ShowStatus($"已将「{item.DisplayName}」加入在看/在读/在玩。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"加入收藏失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async System.Threading.Tasks.Task SearchAsync()
    {
        var keyword = KeywordBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            ShowStatus("请输入搜索关键词。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            ShowStatus("正在搜索...", InfoBarSeverity.Informational);
            var type = (SearchTypeBox.SelectedItem as OptionItem<int?>)?.Value;
            ResultList.ItemsSource = await AppServices.ApiClient.SearchAsync(keyword, type);
            StatusBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            ShowStatus($"搜索失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async System.Threading.Tasks.Task ShowPersonAsync(SearchResultItem item)
    {
        var dialog = new ContentDialog
        {
            Title = item.DisplayName,
            Content = string.IsNullOrWhiteSpace(item.Summary) ? item.Subtitle : item.Summary,
            CloseButtonText = "关闭",
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }
}
