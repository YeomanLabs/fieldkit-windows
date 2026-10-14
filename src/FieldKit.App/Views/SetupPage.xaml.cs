using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace FieldKit.App.Views;

/// <summary>First-launch tenant setup: the two IDs, the registration guide, and demo data.</summary>
public sealed partial class SetupPage : Page
{
    private readonly DispatcherTimer _copiedReset = new() { Interval = TimeSpan.FromSeconds(2) };

    public SetupPage()
    {
        ViewModel = new SetupViewModel(AppServices.Current.Session);
        InitializeComponent();
        _copiedReset.Tick += (_, _) =>
        {
            _copiedReset.Stop();
            CopyStepsIcon.Glyph = "";
            ShowCopyStepsText("Copy the steps for an Entra admin");
        };
        Loaded += (_, _) => TenantBox.Focus(FocusState.Programmatic);
        Unloaded += (_, _) => ViewModel.Detach();
    }

    public SetupViewModel ViewModel { get; }

    /// <summary>The whole guide as text, with the client ID filled in if typed, ready to paste into a message.</summary>
    private void CopySteps_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(ViewModel.Guide.PlainText);
        Clipboard.SetContent(package);
        CopyStepsIcon.Glyph = "";
        ShowCopyStepsText("Copied");
        _copiedReset.Start();
    }

    /// <summary>The button's content is a panel, so Narrator needs its name set alongside the text.</summary>
    private void ShowCopyStepsText(string text)
    {
        CopyStepsText.Text = text;
        AutomationProperties.SetName(CopyStepsButton, text);
    }
}
