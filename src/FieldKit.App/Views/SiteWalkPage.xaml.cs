using System.ComponentModel;
using FieldKit.Core.Persistence;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace FieldKit.App.Views;

/// <summary>
/// An active site walk (ports <c>SiteWalkView</c>). The serial box keeps focus between scans, so a
/// USB scanner can go device after device; each scan's feedback is announced to Narrator.
/// </summary>
public sealed partial class SiteWalkPage : Page
{
    private ScanItem? _menuScan;

    public SiteWalkPage()
    {
        InitializeComponent();
    }

    public SiteWalkViewModel ViewModel { get; private set; } = null!;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        var services = AppServices.Current;
        ViewModel = new SiteWalkViewModel((SiteWalk)e.Parameter, services.Store, services.Session.Lookup, services.Settings, services.Session.SessionExpired);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Bindings.Update();
        Entry.FocusBox();
        await ViewModel.ResumePendingAsync();
    }

    private async void Entry_SerialSubmitted(object? sender, string serial) => await ViewModel.Add(serial);

    private async void Entry_ListPasted(object? sender, string text) => await ViewModel.AddListAsync(text);

    private void Scan_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ScanItem scan) Frame.Navigate(typeof(DevicePage), new DeviceRoute(scan.Serial));
    }

    private void Summary_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(SiteWalkSummaryPage), ViewModel.Walk);

    private async void End_Click(object sender, RoutedEventArgs e)
    {
        if (!await Dialogs.ConfirmAsync(XamlRoot, "End this site walk?", "You can still export it afterwards. Scanning stops.", "End and show summary")) return;
        ViewModel.End();
        Frame.Navigate(typeof(SiteWalkSummaryPage), ViewModel.Walk);
        // Back from the summary goes to where the walk was opened from, not the ended walk.
        var entries = Frame.BackStack;
        if (entries.Count > 0 && entries[^1].SourcePageType == typeof(SiteWalkPage)) entries.RemoveAt(entries.Count - 1);
    }

    private void ScanList_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (ScanFor(args.OriginalSource as DependencyObject) is not { } scan) return;
        args.Handled = true;
        _menuScan = scan;
        RetryItem.Visibility = scan.IsFailed ? Visibility.Visible : Visibility.Collapsed;
        var flyout = (MenuFlyout)Resources["ScanMenu"];
        var target = args.OriginalSource as FrameworkElement ?? ScanList;
        if (args.TryGetPosition(target, out var point)) flyout.ShowAt(target, new FlyoutShowOptions { Position = point });
        else flyout.ShowAt(target);
    }

    private async void RetryScan_Click(object sender, RoutedEventArgs e)
    {
        if (_menuScan is { } scan) await ViewModel.Retry(scan);
    }

    private void DeleteScan_Click(object sender, RoutedEventArgs e)
    {
        if (_menuScan is { } scan) ViewModel.Delete(scan);
        Entry.FocusBox();
    }

    /// <summary>Narrator reads each scan's feedback, standing in for iOS's haptic.</summary>
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SiteWalkViewModel.Banner) || ViewModel.Banner is not { } banner) return;
        var peer = FrameworkElementAutomationPeer.FromElement(Banner) ?? FrameworkElementAutomationPeer.CreatePeerForElement(Banner);
        peer?.RaiseNotificationEvent(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent, banner.Text, "ScanBanner");
    }

    private static ScanItem? ScanFor(DependencyObject? source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { DataContext: ScanItem scan }) return scan;
        }
        return null;
    }
}
