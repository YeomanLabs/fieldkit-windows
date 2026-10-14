using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FieldKit.App.Views;

/// <summary>A person, a link to their groups and assignments, their recent sign-ins, and the Intune devices they're the primary user of.</summary>
public sealed partial class PersonPage : Page
{
    public PersonPage()
    {
        InitializeComponent();
    }

    public PersonViewModel ViewModel { get; private set; } = null!;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        var session = AppServices.Current.Session;
        ViewModel = new PersonViewModel((DirectoryUser)e.Parameter, session.Directory, session.SessionExpired, session.SignIns);
        Bindings.Update();
        await ViewModel.LoadAsync();
    }

    private void Assignments_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(AssignmentsPage), new AssignmentSubject.Person(ViewModel.Person));

    private void SignIn_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SignInItem item) Frame.Navigate(typeof(SignInDetailPage), item.SignIn);
    }

    private void Device_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is IntuneDevice device && DeviceText.LookupSerial(device) is { } serial)
        {
            Frame.Navigate(typeof(DevicePage), new DeviceRoute(serial, device.Id));
        }
    }
}
