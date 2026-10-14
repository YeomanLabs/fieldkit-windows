using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FieldKit.App.Views;

/// <summary>
/// Opens a device reached through a group (ports <c>EntraDeviceView</c>): once the Intune record
/// is found, the device page replaces this one, so Back returns to the members list.
/// </summary>
public sealed partial class EntraDevicePage : Page
{
    public EntraDevicePage()
    {
        InitializeComponent();
    }

    public EntraDeviceViewModel ViewModel { get; private set; } = null!;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        var route = (EntraDeviceRoute)e.Parameter;
        var session = AppServices.Current.Session;
        ViewModel = new EntraDeviceViewModel(route.DeviceId, route.Name, session.Lookup, session.SessionExpired);
        Bindings.Update();
        await ViewModel.ResolveAsync();
        if (ViewModel.State.Kind != SectionKind.Loaded || !ReferenceEquals(Frame.Content, this)) return;
        var device = ViewModel.State.Value!;
        Frame.Navigate(typeof(DevicePage), new DeviceRoute(device.SerialNumber, device.Id));
        // Drop this page from history: Back from the device goes to the members.
        var entries = Frame.BackStack;
        if (entries.Count > 0 && entries[^1].SourcePageType == typeof(EntraDevicePage)) entries.RemoveAt(entries.Count - 1);
    }
}
