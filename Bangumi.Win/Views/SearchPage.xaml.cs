using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Bangumi.Win.Views;

public sealed partial class SearchPage : Page
{
    private const int PageSize = 30;
    private readonly ObservableCollection<SearchResultItem> _results = [];
    private int _offset;
    private bool _hasMore;
    private bool _isLoading;

    public SearchPage()
    {
        InitializeComponent();
        ResultList.ItemsSource = _results;
        foreach (var item in BangumiConstants.SearchTypes)
        {
            var button = new Button
            {
                Content = item.Name,
                Tag = item,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(14, 8, 14, 8)
            };
            button.Click += SearchTypeTab_Click;
            SearchTypeTabs.Children.Add(button);
        }

        if (SearchTypeTabs.Children.FirstOrDefault() is Button first)
        {
            first.IsEnabled = false;
        }

        UpdateTabStyles();
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await SearchAsync(reset: true);

    private async void SearchTypeTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button clicked)
        {
            return;
        }

        foreach (var button in SearchTypeTabs.Children.OfType<Button>())
        {
            button.IsEnabled = true;
        }

        clicked.IsEnabled = false;
        UpdateTabStyles();

        if (IsLoaded && !string.IsNullOrWhiteSpace(KeywordBox.Text))
        {
            await SearchAsync(reset: true);
        }
    }

    private async void KeywordBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await SearchAsync(reset: true);
        }
    }

    private async void ResultList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (_hasMore && !_isLoading && args.ItemIndex >= _results.Count - 6)
        {
            await SearchAsync(reset: false);
        }
    }

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SearchResultItem item })
        {
            Frame.Navigate(item.IsPerson ? typeof(PersonDetailPage) : typeof(SubjectDetailPage), item.Id);
        }
    }

    private async void AddCollection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SearchResultItem item })
        {
            return;
        }

        if (!AppServices.TokenStore.HasToken)
        {
            ShowStatus("请先登录后再加入收藏。", InfoBarSeverity.Warning);
            return;
        }

        if (item.IsPerson)
        {
            await AddPersonCollectionAsync(item);
            return;
        }

        await AddSubjectCollectionAsync(item);
    }

    private async System.Threading.Tasks.Task SearchAsync(bool reset)
    {
        if (_isLoading)
        {
            return;
        }

        var keyword = KeywordBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            ShowStatus("请输入搜索关键词。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            _isLoading = true;
            if (reset)
            {
                _offset = 0;
                _hasMore = true;
                _results.Clear();
            }

            ShowStatus("正在搜索...", InfoBarSeverity.Informational);
            var type = SearchTypeTabs.Children.OfType<Button>().FirstOrDefault(button => !button.IsEnabled)?.Tag is OptionItem<int?> item ? item.Value : null;
            var page = await AppServices.ApiClient.SearchAsync(keyword, type, _offset);
            foreach (var result in page)
            {
                _results.Add(result);
            }

            _offset += page.Count;
            _hasMore = page.Count == PageSize;
            StatusBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            ShowStatus($"搜索失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async System.Threading.Tasks.Task AddSubjectCollectionAsync(SearchResultItem item)
    {
        var statusBox = new ComboBox
        {
            Header = "收藏状态",
            ItemsSource = BangumiConstants.EditableCollectionStatuses,
            DisplayMemberPath = "Name",
            SelectedIndex = 2
        };

        var dialog = new ContentDialog
        {
            Title = $"加入收藏：{item.DisplayName}",
            Content = statusBox,
            PrimaryButtonText = "加入",
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
            await AppServices.ApiClient.AddCollectionAsync(item.Id, status);
            ShowStatus($"已将「{item.DisplayName}」加入收藏。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"加入收藏失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async System.Threading.Tasks.Task AddPersonCollectionAsync(SearchResultItem item)
    {
        try
        {
            await AppServices.ApiClient.CollectPersonAsync(item.Id);
            ShowStatus($"已收藏人物「{item.DisplayName}」。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"收藏人物失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }

    private void UpdateTabStyles()
    {
        var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        foreach (var button in SearchTypeTabs.Children.OfType<Button>())
        {
            button.BorderBrush = button.IsEnabled ? transparent : accent;
            button.Foreground = button.IsEnabled
                ? (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            button.FontWeight = button.IsEnabled ? Microsoft.UI.Text.FontWeights.Normal : Microsoft.UI.Text.FontWeights.SemiBold;
        }
    }
}
