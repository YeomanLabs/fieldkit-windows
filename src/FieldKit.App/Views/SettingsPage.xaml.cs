using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FieldKit.App.Views;

/// <summary>Account, tenant setup, permissions, audit rules, local data and the version.</summary>
public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        var services = AppServices.Current;
        ViewModel = new SettingsViewModel(services.Session, services.Settings, services.Store, services.Version);
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    public string DataFolder => AppServices.Current.DataFolder;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (ViewModel.Permissions is { } permissions) await permissions.LoadAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) => ViewModel.Detach();

    private async void SignOut_Click(object sender, RoutedEventArgs e)
    {
        if (await ConfirmAsync("Sign out?", "Lookups stop until you sign in again.", "Sign out"))
        {
            await ViewModel.SignOutCommand.ExecuteAsync(null);
        }
    }

    private async void ChangeSetup_Click(object sender, RoutedEventArgs e)
    {
        if (await ConfirmAsync("Change tenant setup?", "You'll be signed out first.", "Sign out and change"))
        {
            await ViewModel.ChangeTenantSetupCommand.ExecuteAsync(null);
        }
    }

    private async void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (await ConfirmAsync("Clear all local data?", "Recent lookups and settings on this PC are deleted. Tenant setup is kept.", "Clear"))
        {
            ViewModel.ClearAllLocalDataCommand.Execute(null);
        }
    }

    private async Task<bool> ConfirmAsync(string title, string message, string action)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = action,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
