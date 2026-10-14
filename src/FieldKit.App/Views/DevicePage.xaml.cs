using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FieldKit.App.Views;

/// <summary>
/// A device: the Overview tab (Intune, compliance, Entra and Autopilot, with problems flagged),
/// Groups and assignments, and Hardware, storage and apps; plus the device's commands.
/// </summary>
public sealed partial class DevicePage : Page
{
    /// <summary>Below this content width the cards stack in one column.</summary>
    private const double TwoColumnWidth = 880;

    private DeviceRoute _route = null!;
    private AssignmentsViewModel? _assignments;
    private DeviceInventoryViewModel? _inventory;
    private InstallStatusViewModel? _installStatus;

    public DevicePage()
    {
        InitializeComponent();
    }

    public DeviceDetailViewModel ViewModel { get; private set; } = null!;

    /// <summary>Sync, restart, retire, remediations and recovery secrets for the selected record.</summary>
    public DeviceActionsViewModel Actions { get; private set; } = null!;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        _route = (DeviceRoute)e.Parameter;
        // Read before the tabs' first SelectionChanged (Overview) overwrites it.
        var returnToTab = _route.Tab;
        var services = AppServices.Current;
        ViewModel = new DeviceDetailViewModel(
            _route.Serial,
            _route.PickedIntuneId ?? _route.IntuneId,
            services.Session.Lookup,
            services.Settings,
            services.Store,
            services.Session.SessionExpired);
        Actions = new DeviceActionsViewModel(services.Session.DeviceActions, services.Session.RecoverySecrets, services.OwnerVerifier, services.Session.SessionExpired);
        Bindings.Update();
        await ViewModel.LoadAsync();
        // Back from a group: return to the tab the user left.
        if (returnToTab == 1 && ViewModel.CanShowAssignments) Tabs.SelectedItem = AssignmentsTab;
        if (returnToTab == 2 && ViewModel.HasIntuneRecord) Tabs.SelectedItem = InventoryTab;
        if (returnToTab == 3 && ViewModel.HasIntuneRecord) Tabs.SelectedItem = InstallTab;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ViewModel.Detach();
        _assignments?.Cancel();
        _installStatus?.Cancel();
    }

    /// <summary>F5 refreshes whichever tab is showing.</summary>
    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (Tabs.SelectedItem == AssignmentsTab && _assignments is not null)
        {
            await _assignments.RefreshAsync();
        }
        else if (Tabs.SelectedItem == InventoryTab && _inventory is not null)
        {
            await _inventory.LoadAsync();
        }
        else if (Tabs.SelectedItem == InstallTab && _installStatus is not null)
        {
            await _installStatus.LoadAsync();
        }
        else
        {
            await ViewModel.LoadAsync();
        }
    }

    private async void Tabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var tab = sender.SelectedItem == AssignmentsTab ? 1 : sender.SelectedItem == InventoryTab ? 2 : sender.SelectedItem == InstallTab ? 3 : 0;
        _route.Tab = tab;
        OverviewScroll.Visibility = tab == 0 ? Visibility.Visible : Visibility.Collapsed;
        AssignmentsScroll.Visibility = tab == 1 ? Visibility.Visible : Visibility.Collapsed;
        InventoryScroll.Visibility = tab == 2 ? Visibility.Visible : Visibility.Collapsed;
        InstallScroll.Visibility = tab == 3 ? Visibility.Visible : Visibility.Collapsed;
        // Each tab is built on first view, and again if a different Intune record has been picked since.
        var session = AppServices.Current.Session;
        if (tab == 1 && ViewModel.AssignmentSubject is { } subject && _assignments?.Subject != subject)
        {
            _assignments?.Cancel();
            _assignments = new AssignmentsViewModel(subject, session.Directory, session.AssignmentCatalog, session.SessionExpired);
            Assignments.Show(_assignments);
            await _assignments.LoadAsync();
        }
        else if (tab == 2 && ViewModel.SelectedIntune is { } device && _inventory?.Device.Id != device.Id)
        {
            _inventory = new DeviceInventoryViewModel(device, session.InventoryLookup, session.SessionExpired);
            Inventory.Show(_inventory);
            await _inventory.LoadAsync();
        }
        else if (tab == 3 && ViewModel.SelectedIntune is { } target && _installStatus?.Device.Id != target.Id)
        {
            _installStatus?.Cancel();
            _installStatus = new InstallStatusViewModel(target, session.InstallStatus, session.SessionExpired);
            InstallStatus.Show(_installStatus);
            await _installStatus.LoadAsync();
        }
    }

    private void Assignments_GroupInvoked(object? sender, DirectoryGroup group) => Frame.Navigate(typeof(GroupPage), group);

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedIntune is { } device) await Actions.RunAsync(DeviceAction.Sync, device);
    }

    private async void InstallApp_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedIntune is not { } device) return;
        await new InstallAppDialog(device, ViewModel.EntraObjectId) { XamlRoot = XamlRoot }.ShowAsync();
    }

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedIntune is not { } device) return;
        var confirmed = await Dialogs.ConfirmAsync(XamlRoot, $"Restart {device.DeviceName}?", DeviceAction.Restart.ConfirmationMessage(), "Restart");
        if (confirmed) await Actions.RunAsync(DeviceAction.Restart, device);
    }

    private async void Retire_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedIntune is not { } device) return;
        if (!await Dialogs.ConfirmRetireAsync(XamlRoot, device.DeviceName)) return;
        if (await Actions.RunAsync(DeviceAction.Retire, device)) await ViewModel.LoadIntuneAsync();
    }

    private async void Remediation_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedIntune is not { } device) return;
        var picker = new RemediationPickerDialog(device) { XamlRoot = XamlRoot };
        await picker.ShowAsync();
        if (picker.Picked is not { } script) return;
        if (!await Dialogs.ConfirmAsync(XamlRoot, $"Run {script.Name} on {device.DeviceName}?", "The device runs it the next time it checks in.", "Run now")) return;
        await Actions.RunRemediationAsync(script, device);
    }

    private async void Laps_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedIntune is not { } device || ViewModel.EntraDeviceId is not { } entraId) return;
        await Actions.ShowLocalAdminPasswordAsync(entraId, device.DeviceName, ViewModel.EntraDevice?.TrustType);
        await ShowRevealedAsync();
    }

    private async void BitLocker_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedIntune is not { } device || ViewModel.EntraDeviceId is not { } entraId) return;
        await Actions.ShowBitLockerKeysAsync(entraId, ViewModel.EntraObjectId, device.DeviceName, ViewModel.EntraDevice?.TrustType, device.IsEncrypted);
        await ShowRevealedAsync();
    }

    /// <summary>Opens the secrets dialog for what Hello unlocked, then drops the values.</summary>
    private async Task ShowRevealedAsync()
    {
        if (Actions.Revealed is not { } secrets) return;
        try
        {
            await new SecretsDialog(secrets, App.Window) { XamlRoot = XamlRoot }.ShowAsync();
        }
        finally
        {
            Actions.ForgetRevealed();
        }
    }

    private void ActionStatusBar_Closed(InfoBar sender, InfoBarClosedEventArgs args) => Actions.ClearStatus();

    private async void EditExtensionAttributes_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.EntraDevice is not { } device) return;
        var dialog = new ExtensionAttributesDialog(device) { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
        if (dialog.ViewModel.Saved is { } saved) ViewModel.ExtensionAttributesSaved(saved);
    }

    private async void EditAutopilot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AutopilotDevice record }) return;
        var dialog = new AutopilotEditDialog(record) { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
        if (dialog.Saved) await ViewModel.AutopilotSavedAsync();
    }

    private async void RecordPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecordPicker.SelectedItem is not IntuneDevice device) return;
        _route.PickedIntuneId = device.Id;
        await ViewModel.SelectIntuneRecordAsync(device.Id);
    }

    private void Cards_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= TwoColumnWidth;
        SecondColumn.Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Cards.ColumnSpacing = wide ? 16 : 0;
        Grid.SetColumn(RightColumn, wide ? 1 : 0);
        Grid.SetRow(RightColumn, wide ? 0 : 1);
    }
}
