using Bangumi.Win.Models;
using Bangumi.Win.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bangumi.Win.Views;

public sealed partial class CollectionsPage : Page
{
    private readonly ObservableCollection<SubjectCollection> _collections = [];
    private int _offset;
    private int _total;
    private bool _isLoading;
    private string? _username;
    private CancellationTokenSource? _loadCts;
    private bool _isSubscribedToAuthChanges;

    public CollectionsPage()
    {
        InitializeComponent();
        CollectionList.ItemsSource = _collections;
        CreateTabs(SubjectTypeTabs, BangumiConstants.CollectionSubjectTypes);
        CreateTabs(CollectionStatusTabs, BangumiConstants.GetCollectionStatuses(GetSelectedValue(SubjectTypeTabs)));
        Loaded += CollectionsPage_Loaded;
        Unloaded += CollectionsPage_Unloaded;
    }

    private async void CollectionsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_isSubscribedToAuthChanges)
        {
            AppServices.AuthStateChanged += OnAuthStateChanged;
            _isSubscribedToAuthChanges = true;
        }

        await LoadCollectionsAsync(reset: true);
    }

    private void CollectionsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_isSubscribedToAuthChanges)
        {
            AppServices.AuthStateChanged -= OnAuthStateChanged;
            _isSubscribedToAuthChanges = false;
        }

        CancelCurrentLoad();
    }

    private async void OnAuthStateChanged(object? sender, EventArgs e)
    {
        _username = null;
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
            Frame.Navigate(typeof(SubjectDetailPage), collection);
        }
    }

    private async Task LoadCollectionsAsync(bool reset)
    {
        if (!reset && _isLoading)
        {
            return;
        }

        var requestCts = reset ? ReplaceLoadCancellation() : _loadCts ??= new CancellationTokenSource();
        var subjectType = GetSelectedValue(SubjectTypeTabs);
        var collectionType = GetSelectedValue(CollectionStatusTabs);

        if (reset)
        {
            _offset = 0;
            _total = 0;
            _collections.Clear();
        }

        if (!AppServices.TokenStore.HasToken)
        {
            ShowStatus("请先登录后再查看收藏。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            _isLoading = true;
            ShowStatus("正在加载收藏...", InfoBarSeverity.Informational);
            _username ??= (await AppServices.GetCurrentUserAsync(cancellationToken: requestCts.Token)).Username;
            var result = await AppServices.ApiClient.GetCollectionsAsync(_username, subjectType, collectionType, _offset, requestCts.Token);
            if (!ReferenceEquals(requestCts, _loadCts))
            {
                return;
            }

            _total = result.Total;
            foreach (var item in result.Data)
            {
                _collections.Add(item);
            }

            _offset += result.Data.Count;
            HideStatus();
        }
        catch (OperationCanceledException) when (requestCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ShowStatus($"加载失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            if (ReferenceEquals(requestCts, _loadCts))
            {
                _isLoading = false;
            }
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
        _isLoading = false;
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

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBarHelper.Show(StatusBar, message, severity);
    }

    private void HideStatus()
    {
        StatusInfoBarHelper.Hide(StatusBar);
    }
}
