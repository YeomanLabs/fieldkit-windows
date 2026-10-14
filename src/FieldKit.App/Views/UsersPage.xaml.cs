using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace FieldKit.App.Views;

/// <summary>Every user A–Z, filtered as you type; a person opens their devices and groups.</summary>
public sealed partial class UsersPage : Page, IMenuTab
{
    public UsersPage()
    {
        var session = AppServices.Current.Session;
        Users = new UserListViewModel(session.Directory, session.SessionExpired);
        InitializeComponent();
        ListPaging.LoadMoreNearEnd(UserList, () => Users.CanLoadMore && !Users.HasLoadMoreError, Users.LoadMoreAsync);
    }

    public UserListViewModel Users { get; }

    public void FocusSearchBox()
    {
        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectAll();
    }

    public void StartOver() => _ = Users.StartOverAsync();

    protected override void OnNavigatedTo(NavigationEventArgs e) => _ = MenuStart.Opened(e) ? Users.StartOverAsync() : Users.EnsureLoadedAsync();

    private void Refresh_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        Users.RefreshCommand.Execute(null);
    }

    private void User_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DirectoryUser person) Frame.Navigate(typeof(PersonPage), person);
    }
}
