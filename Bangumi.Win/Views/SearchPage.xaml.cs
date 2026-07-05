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
    private const int PageSize = 20;
    private readonly ObservableCollection<SearchResultItem> _results = [];
    private int _offset;
    private bool _hasMore;
    private bool _isLoading;
    private ScrollViewer? _resultScrollViewer;

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

    private void ResultList_Loaded(object sender, RoutedEventArgs e)
    {
        _resultScrollViewer = FindDescendant<ScrollViewer>(ResultList);
        if (_resultScrollViewer is not null)
        {
            _resultScrollViewer.ViewChanged += ResultScrollViewer_ViewChanged;
        }
    }

    private async void ResultScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_resultScrollViewer is null || !_hasMore || _isLoading)
        {
            return;
        }

        if (_resultScrollViewer.VerticalOffset >= _resultScrollViewer.ScrollableHeight - 240)
        {
            await SearchAsync(reset: false);
        }
    }

    private void ResultList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResultItem item)
        {
            Frame.Navigate(item.IsPerson ? typeof(PersonDetailPage) : typeof(SubjectDetailPage), item.Id);
        }
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
            HideStatus();
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

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusPopupHelper.Show(StatusPopup, StatusBar, message, severity, XamlRoot);
    }

    private void HideStatus()
    {
        StatusPopupHelper.Hide(StatusPopup, StatusBar);
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

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
