using FieldKit.Core.Persistence;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace FieldKit.App.Views;

/// <summary>Site walks: resume, start (site remembered), and history. Past walks open their summary.</summary>
public sealed partial class SiteWalksPage : Page
{
    private SiteWalk? _menuWalk;

    public SiteWalksPage()
    {
        var services = AppServices.Current;
        ViewModel = new SiteWalksViewModel(services.Store, services.Settings);
        InitializeComponent();
        Loaded += (_, _) => SiteBox.Focus(FocusState.Programmatic);
    }

    public SiteWalksViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.Refresh();

    private void Start_Click(object sender, RoutedEventArgs e) => Start();

    /// <summary>Enter in either box starts the walk.</summary>
    private void Field_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || !ViewModel.CanStart) return;
        e.Handled = true;
        Start();
    }

    private void Start()
    {
        if (ViewModel.Start() is { } walk) Frame.Navigate(typeof(SiteWalkPage), walk);
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ActiveWalk is { } walk) Frame.Navigate(typeof(SiteWalkPage), walk);
    }

    private void Walk_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not SiteWalkItem item) return;
        Frame.Navigate(item.IsActive ? typeof(SiteWalkPage) : typeof(SiteWalkSummaryPage), item.Walk);
    }

    private void WalkList_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (ItemFor(args.OriginalSource as DependencyObject) is not { } item) return;
        args.Handled = true;
        _menuWalk = item.Walk;
        var flyout = (MenuFlyout)Resources["WalkMenu"];
        var target = args.OriginalSource as FrameworkElement ?? WalkList;
        if (args.TryGetPosition(target, out var point)) flyout.ShowAt(target, new FlyoutShowOptions { Position = point });
        else flyout.ShowAt(target);
    }

    private async void DeleteWalk_Click(object sender, RoutedEventArgs e)
    {
        if (_menuWalk is not { } walk) return;
        if (!await Dialogs.ConfirmAsync(XamlRoot, $"Delete the {walk.Site} site walk?", "Its scans are deleted from this PC. Exported CSV files aren't affected.", "Delete")) return;
        ViewModel.Delete(walk);
    }

    private static SiteWalkItem? ItemFor(DependencyObject? source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { DataContext: SiteWalkItem item }) return item;
        }
        return null;
    }
}
