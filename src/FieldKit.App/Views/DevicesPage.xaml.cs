using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace FieldKit.App.Views;

/// <summary>
/// Home: every Intune device A–Z, filtered as you type. The box doubles as the serial box
/// (focused at launch; scanners type into it and Enter opens the device), with This PC and
/// recent lookups beside it. Replaces Lookup and Search (the user's decision on 2026-10-13).
/// </summary>
public sealed partial class DevicesPage : Page, IMenuTab
{
    /// <summary>How many recent lookups fit in the row under the box.</summary>
    private const int RecentChips = 5;

    public DevicesPage()
    {
        var services = AppServices.Current;
        var session = services.Session;
        Home = new LookupViewModel(session, services.Store, services.DeviceInfo);
        Devices = new DeviceListViewModel(session.Lookup, session.SessionExpired);
        InitializeComponent();
        foreach (var (serial, shows) in LookupViewModel.DemoSerials)
        {
            var button = new HyperlinkButton { Content = serial, Padding = new Thickness(6, 2, 6, 2) };
            ToolTipService.SetToolTip(button, $"Look up {serial} ({shows})");
            AutomationProperties.SetName(button, $"Look up demo serial {serial}, {shows}");
            button.Click += (_, _) => Open(serial);
            DemoSerials.Items.Add(button);
        }
        ListPaging.LoadMoreNearEnd(DeviceList, () => Devices.CanLoadMore && !Devices.HasLoadMoreError, Devices.LoadMoreAsync);
        Loaded += (_, _) => FocusSearchBox();
        // The page is cached for the session; the shell going away ends it.
        Unloaded += (_, _) =>
        {
            if (AppServices.Current.Session.State != SessionState.SignedIn) Home.Detach();
        };
    }

    public LookupViewModel Home { get; }

    public DeviceListViewModel Devices { get; }

    /// <summary>Ctrl+L and Ctrl+F: back to the box, with what's in it selected so a scan replaces it.</summary>
    public void FocusSearchBox()
    {
        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectAll();
    }

    /// <summary>The Devices menu item while this tab shows: an empty box ready for a scan, and the list afresh.</summary>
    public void StartOver()
    {
        Home.Refresh();
        ShowRecents();
        _ = Devices.StartOverAsync();
        FocusSearchBox();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Home.Refresh();
        ShowRecents();
        _ = MenuStart.Opened(e) ? Devices.StartOverAsync() : Devices.EnsureLoadedAsync();
        if (e.NavigationMode == NavigationMode.Back) FocusSearchBox();
    }

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Scanners finish with Enter, like typing.
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        if (Devices.Submit() is { } target) Open(target.Serial, target.IntuneId);
    }

    private void Refresh_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        Devices.RefreshCommand.Execute(null);
    }

    private void ThisPc_Click(object sender, RoutedEventArgs e)
    {
        if (Home.ThisPcSerial is { } serial) Open(serial);
    }

    private void Device_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is IntuneDevice device && DeviceText.LookupSerial(device) is { } serial) Open(serial, device.Id);
    }

    private void ResumeWalk_Click(object sender, RoutedEventArgs e)
    {
        if (Home.ActiveWalk is { } walk) Frame.Navigate(typeof(SiteWalkPage), walk);
    }

    private void ShowRecents()
    {
        RecentList.Items.Clear();
        foreach (var recent in Home.Recents.Take(RecentChips))
        {
            var button = new HyperlinkButton { Content = recent.Title, Padding = new Thickness(6, 2, 6, 2) };
            ToolTipService.SetToolTip(button, recent.Subtitle);
            AutomationProperties.SetName(button, recent.AutomationName);
            button.Click += (_, _) => Open(recent.Serial);
            RecentList.Items.Add(button);
        }
    }

    private void Open(string serial, string? intuneId = null) => Frame.Navigate(typeof(DevicePage), new DeviceRoute(serial, intuneId));
}
