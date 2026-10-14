using System.ComponentModel;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Views;

/// <summary>
/// Batch Autopilot tagging (ports <c>PreStageView</c>). A USB scanner types into the serial box,
/// which keeps focus between boxes; a pasted list is processed in order.
/// </summary>
public sealed partial class PreStagePage : Page
{
    public PreStagePage()
    {
        var services = AppServices.Current;
        ViewModel = new PreStageViewModel(services.Session.Lookup, services.Session.AutopilotUpdates, services.Settings, services.Session.SessionExpired);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (ViewModel.HasTag) Entry.FocusBox(); else TagBox.Focus(FocusState.Programmatic);
        };
    }

    public PreStageViewModel ViewModel { get; }

    private async void Entry_SerialSubmitted(object? sender, string serial) => await ViewModel.Add(serial);

    private async void Entry_ListPasted(object? sender, string text) => await ViewModel.AddListAsync(text);

    private void SuggestedTag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag }) ViewModel.GroupTagText = tag;
        Entry.FocusBox();
    }

    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PreStageResult result }) await ViewModel.Retry(result);
    }

    /// <summary>Narrator reads each scan's feedback, standing in for iOS's haptic.</summary>
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PreStageViewModel.Banner) || ViewModel.Banner is not { } banner) return;
        var peer = FrameworkElementAutomationPeer.FromElement(Banner) ?? FrameworkElementAutomationPeer.CreatePeerForElement(Banner);
        peer?.RaiseNotificationEvent(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent, banner.Text, "ScanBanner");
    }
}
