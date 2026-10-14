using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace FieldKit.App.Views;

/// <summary>Every group A–Z, filtered as you type; a group opens what targets it and its members.</summary>
public sealed partial class GroupsPage : Page, IMenuTab
{
    public GroupsPage()
    {
        var session = AppServices.Current.Session;
        Groups = new GroupListViewModel(session.Directory, session.SessionExpired);
        InitializeComponent();
        ListPaging.LoadMoreNearEnd(GroupList, () => Groups.CanLoadMore && !Groups.HasLoadMoreError, Groups.LoadMoreAsync);
    }

    public GroupListViewModel Groups { get; }

    public void FocusSearchBox()
    {
        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectAll();
    }

    public void StartOver() => _ = Groups.StartOverAsync();

    protected override void OnNavigatedTo(NavigationEventArgs e) => _ = MenuStart.Opened(e) ? Groups.StartOverAsync() : Groups.EnsureLoadedAsync();

    private void Refresh_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        Groups.RefreshCommand.Execute(null);
    }

    private void Group_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DirectoryGroup group) Frame.Navigate(typeof(GroupPage), group);
    }
}
