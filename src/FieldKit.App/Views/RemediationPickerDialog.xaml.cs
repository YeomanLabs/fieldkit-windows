using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Views;

/// <summary>Picks a remediation to run on a Windows device now (ports <c>RemediationPickerSheet</c>).</summary>
public sealed partial class RemediationPickerDialog : ContentDialog
{
    public RemediationPickerDialog(IntuneDevice device)
    {
        var session = AppServices.Current.Session;
        ViewModel = new RemediationPickerViewModel(session.AssignmentCatalog, session.SessionExpired);
        RetryCommand = new AsyncRelayCommand(() => ViewModel.LoadAsync(refresh: true));
        InitializeComponent();
        Footer.Text = $"Runs the detection script, and the remediation if detection finds a problem, the next time {device.DeviceName} checks in. Needs Windows Hello.";
        Opened += async (_, _) =>
        {
            SearchBox.Focus(FocusState.Programmatic);
            await ViewModel.LoadAsync();
        };
    }

    public RemediationPickerViewModel ViewModel { get; }

    public IAsyncRelayCommand RetryCommand { get; }

    /// <summary>The remediation picked, or null when cancelled.</summary>
    public AssignedItem? Picked { get; private set; }

    private void Script_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not AssignedItem script) return;
        Picked = script;
        Hide();
    }
}
