using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace Bangumi.Win.Views;

public sealed partial class CollectionsPage : Page
{
    private const int PageSize = 30;
    private int _offset;
    private int _total;
    private string? _username;

    public CollectionsPage()
    {
        InitializeComponent();
        SubjectTypeBox.ItemsSource = BangumiConstants.SubjectTypes;
        SubjectTypeBox.SelectedIndex = 0;
        CollectionStatusBox.ItemsSource = BangumiConstants.CollectionStatuses;
        CollectionStatusBox.SelectedIndex = 0;
        Loaded += CollectionsPage_Loaded;
    }

    private async void CollectionsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadCollectionsAsync(resetOffset: true);
    }

    private async void Filters_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            await LoadCollectionsAsync(resetOffset: true);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadCollectionsAsync(resetOffset: true);
    }

    private async void PreviousPage_Click(object sender, RoutedEventArgs e)
    {
        _offset = Math.Max(0, _offset - PageSize);
        await LoadCollectionsAsync(resetOffset: false);
    }

    private async void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_offset + PageSize < _total)
        {
            _offset += PageSize;
            await LoadCollectionsAsync(resetOffset: false);
        }
    }

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SubjectCollection collection })
        {
            Frame.Navigate(typeof(SubjectDetailPage), collection.Subject.Id);
        }
    }

    private async void Manage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SubjectCollection collection })
        {
            return;
        }

        if (collection.Subject.Type == 2)
        {
            Frame.Navigate(typeof(EpisodeProgressPage), collection.Subject);
            return;
        }

        var statusBox = new ComboBox
        {
            Header = "收藏状态",
            ItemsSource = BangumiConstants.EditableCollectionStatuses,
            DisplayMemberPath = "Name",
            SelectedIndex = Math.Max(0, IndexOfStatus(collection.Type))
        };
        var epBox = new NumberBox
        {
            Header = collection.Subject.Type == 1 ? "话数进度" : "章节进度",
            Minimum = 0,
            Maximum = collection.Subject.Eps ?? 10000,
            Value = collection.EpStatus ?? 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            IsEnabled = collection.Subject.Type == 1 && collection.Subject.Eps is > 0
        };
        var volBox = new NumberBox
        {
            Header = "卷数进度",
            Minimum = 0,
            Maximum = collection.Subject.Volumes ?? 10000,
            Value = collection.VolStatus ?? 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            IsEnabled = collection.Subject.Type == 1 && collection.Subject.Volumes is > 0
        };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(statusBox);
        panel.Children.Add(epBox);
        panel.Children.Add(volBox);
        if (collection.Subject.Type != 1)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "当前 API 仅允许直接修改书籍条目的话数/卷数进度；动画请使用单集进度页面。",
                TextWrapping = TextWrapping.Wrap
            });
        }

        var dialog = new ContentDialog
        {
            Title = collection.Subject.DisplayName,
            Content = panel,
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                var status = statusBox.SelectedItem is OptionItem<int> selected ? selected.Value : collection.Type;
                await AppServices.ApiClient.UpdateCollectionAsync(
                    collection.Subject.Id,
                    status,
                    epBox.IsEnabled ? Convert.ToInt32(epBox.Value) : null,
                    volBox.IsEnabled ? Convert.ToInt32(volBox.Value) : null);
                await LoadCollectionsAsync(resetOffset: false);
                ShowStatus("收藏已更新。", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"更新失败：{ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private async System.Threading.Tasks.Task LoadCollectionsAsync(bool resetOffset)
    {
        if (!AppServices.TokenStore.HasToken)
        {
            ShowStatus("请先登录后再管理收藏。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            if (resetOffset)
            {
                _offset = 0;
            }

            ShowStatus("正在加载收藏...", InfoBarSeverity.Informational);
            if (_username is null)
            {
                _username = (await AppServices.ApiClient.GetMeAsync()).Username;
            }

            var subjectType = (SubjectTypeBox.SelectedItem as OptionItem<int?>)?.Value;
            var status = (CollectionStatusBox.SelectedItem as OptionItem<int?>)?.Value;
            var result = await AppServices.ApiClient.GetCollectionsAsync(_username, subjectType, status, _offset);
            _total = result.Total;
            CollectionList.ItemsSource = result.Data;
            PageText.Text = $"{_offset + 1}-{Math.Min(_offset + result.Data.Count, _total)} / {_total}";
            StatusBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            ShowStatus($"加载失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private static int IndexOfStatus(int status)
    {
        var list = (IReadOnlyList<OptionItem<int>>)BangumiConstants.EditableCollectionStatuses;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Value == status)
            {
                return i;
            }
        }

        return 0;
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }
}
