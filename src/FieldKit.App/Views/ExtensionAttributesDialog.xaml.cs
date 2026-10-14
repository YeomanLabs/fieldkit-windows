using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Views;

/// <summary>Edit an Entra device's extensionAttribute1–15 (Windows only; added 2026-10-13).</summary>
public sealed partial class ExtensionAttributesDialog : ContentDialog
{
    public ExtensionAttributesDialog(EntraDevice device)
    {
        var session = AppServices.Current.Session;
        ViewModel = new ExtensionAttributesEditViewModel(device, session.EntraDeviceUpdates, session.SessionExpired);
        Intro = $"{device.DisplayName} in Entra. Dynamic groups and assignment filters that use these follow a change within minutes, so the apps and policies that reach the device can change too. Empty a box to clear it.";
        InitializeComponent();
    }

    public ExtensionAttributesEditViewModel ViewModel { get; }

    public string Intro { get; }

    private async void Save_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            // Stay open to show what went wrong.
            args.Cancel = !await ViewModel.SaveAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }
}
