using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Views;

/// <summary>Confirmations shared by pages: a yes/no question, and retire's typed device name.</summary>
internal static class Dialogs
{
    /// <summary>
    /// Shows a dialog in the theme the pages are in (Settings → Appearance). Dialogs sit outside
    /// the page tree, so on their own they'd follow Windows instead. Every dialog opens this way.
    /// </summary>
    public static Windows.Foundation.IAsyncOperation<ContentDialogResult> ShowThemedAsync(this ContentDialog dialog)
    {
        dialog.RequestedTheme = App.Window.ActualTheme;
        return dialog.ShowAsync();
    }

    /// <summary>Cancel is the default, so Enter never confirms something destructive by accident.</summary>
    public static async Task<bool> ConfirmAsync(XamlRoot root, string title, string message, string confirm)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = confirm,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowThemedAsync() == ContentDialogResult.Primary;
    }

    /// <summary>Retire is irreversible, so the device name has to be typed back before Windows Hello (ports <c>RetireConfirmSheet</c>).</summary>
    public static async Task<bool> ConfirmRetireAsync(XamlRoot root, string deviceName)
    {
        var typed = new TextBox
        {
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["MonospaceFont"],
            IsSpellCheckEnabled = false,
            PlaceholderText = deviceName,
            Header = new TextBlock { Text = "Type the device name to confirm", Style = (Style)Application.Current.Resources["FieldHeaderStyle"] },
        };
        AutomationProperties.SetName(typed, "Type the device name to confirm");
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = DeviceAction.Retire.ConfirmationMessage(), TextWrapping = TextWrapping.Wrap });
        content.Children.Add(typed);
        content.Children.Add(new TextBlock { Text = "You'll be asked for Windows Hello next.", Style = (Style)Application.Current.Resources["CaptionSecondaryStyle"] });
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = $"Retire {deviceName}",
            Content = content,
            PrimaryButtonText = "Retire device",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
        };
        typed.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = DeviceActionsViewModel.RetireNameMatches(typed.Text, deviceName);
        dialog.Opened += (_, _) => typed.Focus(FocusState.Programmatic);
        return await dialog.ShowThemedAsync() == ContentDialogResult.Primary;
    }
}
