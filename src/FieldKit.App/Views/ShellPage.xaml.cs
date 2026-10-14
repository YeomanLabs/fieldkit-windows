using System.ComponentModel;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace FieldKit.App.Views;

/// <summary>The signed-in app: navigation on the left, pages on the right, keyboard shortcuts everywhere.</summary>
public sealed partial class ShellPage : Page
{
    private readonly MainWindow _window;
    private readonly AppSession _session = AppServices.Current.Session;
    private bool _syncingSelection;

    internal ShellPage(MainWindow window)
    {
        _window = window;
        InitializeComponent();
        _session.PropertyChanged += SessionChanged;
        Unloaded += (_, _) => _session.PropertyChanged -= SessionChanged;
        ShowAccount();
        ContentFrame.Navigate(typeof(DevicesPage));
    }

    internal void GoBack()
    {
        if (ContentFrame.CanGoBack) ContentFrame.GoBack();
    }

    internal void TogglePane() => Navigation.IsPaneOpen = !Navigation.IsPaneOpen;

    /// <summary>A selection made without a click (Narrator's select, for one): go to the section.</summary>
    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_syncingSelection) return;
        var page = args.IsSettingsSelected ? typeof(SettingsPage) : PageFor(args.SelectedItem);
        if (ContentFrame.CurrentSourcePageType != page) ContentFrame.Navigate(page, MenuStart.Instance);
    }

    /// <summary>
    /// A click on a menu item always goes to its section's page, even when the item is already
    /// selected because a device, person, group or site walk opened from there is showing. The
    /// Devices, Users and Groups tabs start over: the search box empties and the list reloads.
    /// Each tab is one cached page, so after that, Back to an earlier visit shows the fresh list too.
    /// </summary>
    private void Navigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        // The account entry opens Settings, where sign-out and tenant setup are.
        var page = ReferenceEquals(args.InvokedItemContainer, AccountItem) || args.IsSettingsInvoked ? typeof(SettingsPage)
            : PageFor(args.InvokedItemContainer);
        if (ContentFrame.CurrentSourcePageType != page)
        {
            ContentFrame.Navigate(page, MenuStart.Instance);
        }
        else
        {
            (ContentFrame.Content as IMenuTab)?.StartOver();
        }
    }

    private Type PageFor(object? item) =>
        ReferenceEquals(item, UsersItem) ? typeof(UsersPage)
        : ReferenceEquals(item, GroupsItem) ? typeof(GroupsPage)
        : ReferenceEquals(item, PreStageItem) ? typeof(PreStagePage)
        : ReferenceEquals(item, SiteWalksItem) ? typeof(SiteWalksPage)
        : typeof(DevicesPage);

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        _window.SetBackButton(ContentFrame.CanGoBack);
        // Device and person pages keep whichever section they were opened from.
        object? item = e.SourcePageType == typeof(DevicesPage) ? DevicesItem
            : e.SourcePageType == typeof(UsersPage) ? UsersItem
            : e.SourcePageType == typeof(GroupsPage) ? GroupsItem
            : e.SourcePageType == typeof(PreStagePage) ? PreStageItem
            : e.SourcePageType == typeof(SiteWalksPage) || e.SourcePageType == typeof(SiteWalkPage) || e.SourcePageType == typeof(SiteWalkSummaryPage) ? SiteWalksItem
            : e.SourcePageType == typeof(SettingsPage) ? Navigation.SettingsItem
            : null;
        if (item is null || ReferenceEquals(Navigation.SelectedItem, item)) return;
        _syncingSelection = true;
        Navigation.SelectedItem = item;
        _syncingSelection = false;
    }

    /// <summary>Ctrl+L: the Devices box, ready for a scan.</summary>
    private void FocusDevices_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ContentFrame.Content is not DevicesPage) ContentFrame.Navigate(typeof(DevicesPage));
        (ContentFrame.Content as DevicesPage)?.FocusSearchBox();
    }

    /// <summary>Ctrl+F: the search box of the Users or Groups tab when one is showing, otherwise Devices.</summary>
    private void FocusSearch_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        switch (ContentFrame.Content)
        {
            case UsersPage users:
                users.FocusSearchBox();
                break;
            case GroupsPage groups:
                groups.FocusSearchBox();
                break;
            default:
                FocusDevices_Invoked(sender, args);
                break;
        }
    }

    private void Back_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!ContentFrame.CanGoBack) return;
        args.Handled = true;
        ContentFrame.GoBack();
    }

    /// <summary>The mouse's back button goes back too.</summary>
    private void Page_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse) return;
        if (!e.GetCurrentPoint(this).Properties.IsXButton1Pressed || !ContentFrame.CanGoBack) return;
        e.Handled = true;
        ContentFrame.GoBack();
    }

    private void SessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSession.User)) ShowAccount();
    }

    private void ShowAccount()
    {
        var user = _session.User;
        AccountName.Text = user?.DisplayName ?? "";
        AccountTenant.Text = _session.IsDemo ? "Demo data" : user?.TenantName ?? user?.TenantId ?? "";
        var summary = $"Signed in as {AccountName.Text}, {AccountTenant.Text}. Opens settings.";
        AutomationProperties.SetName(AccountItem, summary);
        ToolTipService.SetToolTip(AccountItem, $"{user?.Username}\n{AccountTenant.Text}");
    }
}
