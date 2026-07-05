using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Bangumi.Win.Views;

public sealed partial class CollectionsPage : Page
{
    private const int PageSize = 30;
    private readonly ObservableCollection<SubjectCollection> _collections = [];
    private int _offset;
    private int _total;
    private bool _isLoading;
    private string? _username;

    public CollectionsPage()
    {
        InitializeComponent();
        CollectionList.ItemsSource = _collections;
        CreateTabs(SubjectTypeTabs, BangumiConstants.SubjectTypes);
        CreateTabs(CollectionStatusTabs, BangumiConstants.CollectionStatuses);
        Loaded += CollectionsPage_Loaded;
    }

    private async void CollectionsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadCollectionsAsync(reset: true);
    }

    private async void SubjectTypeTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            await LoadCollectionsAsync(reset: true);
        }
    }

    private async void CollectionStatusTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            await LoadCollectionsAsync(reset: true);
        }
    }

    private async void CollectionList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!_isLoading && _collections.Count < _total && args.ItemIndex >= _collections.Count - 6)
        {
            await LoadCollectionsAsync(reset: false);
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
                await LoadCollectionsAsync(reset: true);
                ShowStatus("收藏已更新。", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"更新失败：{ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private async System.Threading.Tasks.Task LoadCollectionsAsync(bool reset)
    {
        if (_isLoading)
        {
            return;
        }

        if (!AppServices.TokenStore.HasToken)
        {
            ShowStatus("请先登录后再管理收藏。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            _isLoading = true;
            if (reset)
            {
                _offset = 0;
                _total = 0;
                _collections.Clear();
            }

            ShowStatus("正在加载收藏...", InfoBarSeverity.Informational);
            _username ??= (await AppServices.ApiClient.GetMeAsync()).Username;
            var result = await AppServices.ApiClient.GetCollectionsAsync(_username, GetSelectedValue(SubjectTypeTabs), GetSelectedValue(CollectionStatusTabs), _offset);
            _total = result.Total;
            foreach (var item in result.Data)
            {
                _collections.Add(item);
            }

            _offset += result.Data.Count;
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

    private static void CreateTabs(TabView tabView, IReadOnlyList<OptionItem<int?>> items)
    {
        foreach (var item in items)
        {
            tabView.TabItems.Add(new TabViewItem
            {
                Header = item.Name,
                Tag = item,
                IsClosable = false
            });
        }

        tabView.SelectedIndex = 0;
    }

    private static int? GetSelectedValue(TabView tabView)
    {
        return tabView.SelectedItem is TabViewItem { Tag: OptionItem<int?> item } ? item.Value : null;
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
