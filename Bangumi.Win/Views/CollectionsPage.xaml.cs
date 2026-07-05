using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
        CreateTabs(SubjectTypeTabs, BangumiConstants.CollectionSubjectTypes);
        CreateTabs(CollectionStatusTabs, BangumiConstants.GetCollectionStatuses(GetSelectedValue(SubjectTypeTabs)));
        Loaded += CollectionsPage_Loaded;
    }

    private async void CollectionsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadCollectionsAsync(reset: true);
    }

    private async void CollectionList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!_isLoading && _collections.Count < _total && args.ItemIndex >= _collections.Count - 6)
        {
            await LoadCollectionsAsync(reset: false);
        }
    }

    private void CollectionList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SubjectCollection collection)
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

        var statusBox = new ComboBox
        {
            Header = "收藏状态",
            ItemsSource = BangumiConstants.GetEditableCollectionStatuses(collection.Subject.Type),
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
        if (collection.Subject.Type == 1)
        {
            panel.Children.Add(epBox);
            panel.Children.Add(volBox);
        }
        else if (collection.Subject.Type == 2)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "这里修改整个动画的收藏状态；单集进度请使用单集进度入口。",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
        }

        var dialog = new ContentDialog
        {
            Title = collection.Subject.DisplayName,
            Content = panel,
            PrimaryButtonText = "保存",
            SecondaryButtonText = collection.Subject.Type == 2 ? "单集进度" : string.Empty,
            CloseButtonText = "取消",
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary)
        {
            Frame.Navigate(typeof(EpisodeProgressPage), collection.Subject);
            return;
        }

        if (result == ContentDialogResult.Primary)
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

    private void CreateTabs(StackPanel tabHost, IReadOnlyList<OptionItem<int?>> items)
    {
        foreach (var item in items)
        {
            var button = new Button
            {
                Content = item.Name,
                Tag = item,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(14, 8, 14, 8)
            };
            button.Click += FilterTab_Click;
            tabHost.Children.Add(button);
        }

        if (tabHost.Children.FirstOrDefault() is Button first)
        {
            first.IsEnabled = false;
        }

        UpdateTabStyles(tabHost);
    }

    private async void FilterTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button clicked)
        {
            return;
        }

        var parent = clicked.Parent as StackPanel;
        if (parent is null)
        {
            return;
        }

        foreach (var button in parent.Children.OfType<Button>())
        {
            button.IsEnabled = true;
        }

        clicked.IsEnabled = false;
        UpdateTabStyles(parent);

        if (parent == SubjectTypeTabs)
        {
            CollectionStatusTabs.Children.Clear();
            CreateTabs(CollectionStatusTabs, BangumiConstants.GetCollectionStatuses(GetSelectedValue(SubjectTypeTabs)));
        }

        if (IsLoaded)
        {
            await LoadCollectionsAsync(reset: true);
        }
    }

    private static int? GetSelectedValue(StackPanel tabHost)
    {
        return tabHost.Children.OfType<Button>().FirstOrDefault(button => !button.IsEnabled)?.Tag is OptionItem<int?> item ? item.Value : null;
    }

    private void UpdateTabStyles(StackPanel tabHost)
    {
        var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        foreach (var button in tabHost.Children.OfType<Button>())
        {
            button.BorderBrush = button.IsEnabled ? transparent : accent;
            button.Foreground = button.IsEnabled
                ? (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            button.FontWeight = button.IsEnabled ? Microsoft.UI.Text.FontWeights.Normal : Microsoft.UI.Text.FontWeights.SemiBold;
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
