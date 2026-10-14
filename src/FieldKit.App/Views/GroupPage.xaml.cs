using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FieldKit.App.Views;

/// <summary>A group: its type and rule, and what Intune targets at it (ports <c>GroupView</c>).</summary>
public sealed partial class GroupPage : Page
{
    public GroupPage()
    {
        InitializeComponent();
    }

    public GroupViewModel ViewModel { get; private set; } = null!;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        var session = AppServices.Current.Session;
        ViewModel = new GroupViewModel((DirectoryGroup)e.Parameter, session.AssignmentCatalog, session.SessionExpired);
        Bindings.Update();
        await ViewModel.LoadAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) => ViewModel.Cancel();

    private void Members_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(GroupMembersPage), ViewModel.Group);
}
