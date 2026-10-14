using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Views;

/// <summary>
/// Pick an Intune app for this device's platform, then a Required group to join (ports
/// <c>InstallAppSheet</c> and <c>AppInstallOptionsView</c>). One dialog can't open another, so
/// adding and removing are confirmed in a flyout on the button.
/// </summary>
public sealed partial class InstallAppDialog : ContentDialog
{
    public InstallAppDialog(IntuneDevice device, string? entraObjectId)
    {
        var session = AppServices.Current.Session;
        ViewModel = new InstallAppViewModel(device, entraObjectId, session.AssignmentCatalog, session.Directory,
            session.GroupMembershipUpdates, session.DeviceActions, session.SessionExpired);
        RetryCommand = new AsyncRelayCommand(() => ViewModel.LoadAsync(refresh: true));
        RetryOptionsCommand = new AsyncRelayCommand(() => ViewModel.SelectedApp is { } app ? ViewModel.LoadOptionsAsync(app) : Task.CompletedTask);
        InitializeComponent();
        Opened += async (_, _) =>
        {
            SearchBox.Focus(FocusState.Programmatic);
            await ViewModel.LoadAsync();
        };
    }

    public InstallAppViewModel ViewModel { get; }

    public IAsyncRelayCommand RetryCommand { get; }

    public IAsyncRelayCommand RetryOptionsCommand { get; }

    private async void App_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is InstallAppRow row) await ViewModel.SelectAsync(row.App);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearSelection();
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void AddDevice_Click(object sender, RoutedEventArgs e) => ConfirmAdd(sender, InstallMember.Device);

    private void AddUser_Click(object sender, RoutedEventArgs e) => ConfirmAdd(sender, InstallMember.User);

    private void RemoveDevice_Click(object sender, RoutedEventArgs e) => ConfirmRemove(sender, InstallMember.Device);

    private void RemoveUser_Click(object sender, RoutedEventArgs e) => ConfirmRemove(sender, InstallMember.User);

    private async void Sync_Click(object sender, RoutedEventArgs e) => await ViewModel.SyncDeviceAsync();

    private void ConfirmAdd(object sender, InstallMember member)
    {
        if (sender is not Button { Tag: InstallOptionRow row } button || ViewModel.SelectedApp is not { } app) return;
        Confirm(button, ViewModel.AddTitle(member, row.Group), ViewModel.AddMessage(row.Option, app), "Add",
            () => ViewModel.AddAsync(member, row.Group, app));
    }

    private void ConfirmRemove(object sender, InstallMember member)
    {
        if (sender is not Button { Tag: InstallOptionRow row } button) return;
        Confirm(button, ViewModel.RemoveTitle(member, row.Group), GroupMembersViewModel.RemovalMessage, "Remove",
            () => ViewModel.RemoveAsync(member, row.Group));
    }

    /// <summary>A small confirmation on the button itself (iOS uses a confirmation dialog).</summary>
    private static void Confirm(Button button, string title, string message, string action, Func<Task> run)
    {
        var flyout = new Flyout();
        var confirm = new Button { Content = action, Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        confirm.Click += async (_, _) =>
        {
            flyout.Hide();
            await run();
        };
        var panel = new StackPanel { MaxWidth = 360, Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(confirm);
        flyout.Content = panel;
        flyout.ShowAt(button);
    }
}
