using FieldKit.App.Controls;
using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace FieldKit.App.Views;

/// <summary>
/// A group's direct members (ports <c>GroupMembersView</c>). People, devices and nested groups
/// open; "Remove from group…" is on each member's context menu instead of iOS's swipe.
/// </summary>
public sealed partial class GroupMembersPage : Page
{
    private GroupMember? _menuMember;

    public GroupMembersPage()
    {
        InitializeComponent();
    }

    public GroupMembersViewModel ViewModel { get; private set; } = null!;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        var session = AppServices.Current.Session;
        ViewModel = new GroupMembersViewModel((DirectoryGroup)e.Parameter, session.Directory, session.GroupMembershipUpdates, session.SessionExpired);
        ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(GroupMembersViewModel.Removing)) DimRemovingRow();
        };
        Bindings.Update();
        await ViewModel.LoadAsync();
    }

    /// <summary>The member being removed is dimmed until Graph answers, as on iOS.</summary>
    private void DimRemovingRow()
    {
        foreach (var member in ViewModel.MemberList)
        {
            if (MemberList.ContainerFromItem(member) is ListViewItem { ContentTemplateRoot: MemberRow row })
            {
                row.IsRemoving = member.Id == ViewModel.Removing;
            }
        }
    }

    private void Member_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not GroupMember member) return;
        switch (member.Kind)
        {
            case GroupMemberKind.User user:
                Frame.Navigate(typeof(PersonPage), new DirectoryUser { Id = member.Id, DisplayName = member.DisplayName, UserPrincipalName = user.UserPrincipalName });
                break;
            case GroupMemberKind.Device { DeviceId: { } deviceId }:
                Frame.Navigate(typeof(EntraDevicePage), new EntraDeviceRoute(deviceId, member.DisplayName));
                break;
            case GroupMemberKind.Group:
                Frame.Navigate(typeof(GroupPage), new DirectoryGroup { Id = member.Id, DisplayName = member.DisplayName, SecurityEnabled = true });
                break;
        }
    }

    /// <summary>Right-click, Shift+F10 or the menu key on a member offers removal when it's allowed.</summary>
    private void MemberList_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (MemberFor(args.OriginalSource as DependencyObject) is not { } member || !ViewModel.CanRemove(member)) return;
        args.Handled = true;
        _menuMember = member;
        var flyout = (MenuFlyout)Resources["MemberMenu"];
        var target = args.OriginalSource as FrameworkElement ?? MemberList;
        if (args.TryGetPosition(target, out var point))
        {
            flyout.ShowAt(target, new FlyoutShowOptions { Position = point });
        }
        else
        {
            flyout.ShowAt(target);
        }
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_menuMember is not { } member) return;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = GroupMembersViewModel.RemovalTitle(member, ViewModel.Group),
            Content = new TextBlock { Text = GroupMembersViewModel.RemovalMessage, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await ViewModel.RemoveAsync(member);
    }

    private static GroupMember? MemberFor(DependencyObject? source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is MemberRow { Member: { } member }) return member;
            if (current is FrameworkElement { DataContext: GroupMember context }) return context;
        }
        return null;
    }
}
