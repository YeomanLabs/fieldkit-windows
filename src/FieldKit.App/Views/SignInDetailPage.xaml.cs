using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FieldKit.App.Views;

/// <summary>One sign-in explained: the verdict, what to check, the policies and the device (opened from a person).</summary>
public sealed partial class SignInDetailPage : Page
{
    public SignInDetailPage()
    {
        InitializeComponent();
    }

    public SignInDetailViewModel ViewModel { get; private set; } = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = new SignInDetailViewModel((SignIn)e.Parameter);
        Bindings.Update();
    }

    private void ShowAllPolicies_Click(object sender, RoutedEventArgs e) => ViewModel.ShowsAllPolicies = !ViewModel.ShowsAllPolicies;

    /// <summary>Finds the device's Intune record by its Entra device ID, then opens the device page.</summary>
    private void OpenDevice_Click(object sender, RoutedEventArgs e)
    {
        var device = ViewModel.SignIn.Device;
        if (!device.HasIdentity) return;
        Frame.Navigate(typeof(EntraDevicePage), new EntraDeviceRoute(device.DeviceId, device.DisplayName.Length > 0 ? device.DisplayName : device.DeviceId));
    }
}
