using Bangumi.Win.Services;
using Bangumi.Win.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bangumi.Win;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AppServices.AuthStateChanged += OnAuthStateChanged;
        UpdateAccountItem();
        Navigate(AppServices.TokenStore.HasToken ? "home" : "account");
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            Navigate(tag);
        }
    }

    private void Navigate(string tag)
    {
        var pageType = tag switch
        {
            "home" => typeof(HomePage),
            "collections" => typeof(CollectionsPage),
            "search" => typeof(SearchPage),
            "account" => typeof(LoginPage),
            _ => typeof(HomePage)
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }

    private void OnAuthStateChanged(object? sender, System.EventArgs e) => UpdateAccountItem();

    private void UpdateAccountItem()
    {
        AccountNavigationItem.Content = AppServices.TokenStore.HasToken ? "账户" : "登录";
    }
}
