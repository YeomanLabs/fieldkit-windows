using FieldKit.App.Views;
using FieldKit.Core.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace FieldKit.App.Controls;

/// <summary>
/// Continuous serial entry (ports <c>SerialEntryField</c> with <c>keepsFocus</c>), plus pasting a
/// list (SPEC-windows §7). Raises <see cref="SerialSubmitted"/> with a normalised serial, or
/// <see cref="ListPasted"/> with the pasted text.
/// </summary>
public sealed partial class SerialEntryBox : UserControl
{
    public const string InvalidSerialMessage = "That doesn't look like a serial (5–30 letters, numbers or dashes).";

    public SerialEntryBox()
    {
        InitializeComponent();
        AutomationProperties.SetName(Box, "Serial number");
    }

    public event EventHandler<string>? SerialSubmitted;

    public event EventHandler<string>? ListPasted;

    /// <summary>The button's text, e.g. "Add" or "Scan".</summary>
    public string SubmitText
    {
        get => (string)SubmitButton.Content;
        set => SubmitButton.Content = value;
    }

    /// <summary>What the paste dialog says will happen to the list.</summary>
    public string PasteListDescription { get; set; } = "Each serial is processed in order, as if it had been scanned.";

    public void FocusBox() => Box.Focus(FocusState.Programmatic);

    private void Box_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        Submit();
    }

    private void Submit_Click(object sender, RoutedEventArgs e) => Submit();

    private void Box_TextChanged(object sender, TextChangedEventArgs e) => ShowError(null);

    private void Submit()
    {
        if (Box.Text.Trim().Length == 0) return;
        if (SerialNormalizer.Normalize(Box.Text) is not { } serial)
        {
            ShowError(InvalidSerialMessage);
            return;
        }
        Box.Text = "";
        SerialSubmitted?.Invoke(this, serial);
        // Ready for the next scan.
        Box.Focus(FocusState.Programmatic);
    }

    private async void PasteList_Click(object sender, RoutedEventArgs e)
    {
        var text = new TextBox
        {
            AcceptsReturn = true,
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["MonospaceFont"],
            Height = 240,
            IsSpellCheckEnabled = false,
            PlaceholderText = "5CG1234XYZ\nPF2ABCDE\n…",
            TextWrapping = TextWrapping.NoWrap,
        };
        AutomationProperties.SetName(text, "Serials");
        ScrollViewer.SetVerticalScrollBarVisibility(text, ScrollBarVisibility.Auto);
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = $"One per line, or separated by commas or tabs: a column copied from Excel works. Duplicates and entries that aren't serials are skipped. {PasteListDescription}",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(text);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Paste a list of serials",
            Content = content,
            PrimaryButtonText = "Add serials",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.Opened += (_, _) => text.Focus(FocusState.Programmatic);
        if (await dialog.ShowThemedAsync() == ContentDialogResult.Primary && text.Text.Trim().Length > 0)
        {
            ListPasted?.Invoke(this, text.Text);
        }
        Box.Focus(FocusState.Programmatic);
    }

    private void ShowError(string? message)
    {
        ErrorText.Text = message ?? "";
        ErrorText.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
        if (message is null) return;
        var peer = FrameworkElementAutomationPeer.FromElement(ErrorText) ?? FrameworkElementAutomationPeer.CreatePeerForElement(ErrorText);
        peer?.RaiseNotificationEvent(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent, message, "SerialError");
    }
}
