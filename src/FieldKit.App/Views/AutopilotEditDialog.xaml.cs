using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Views;

/// <summary>Edit one Autopilot record's group tag and assigned user (ports <c>AutopilotEditSheet</c>).</summary>
public sealed partial class AutopilotEditDialog : ContentDialog
{
    public AutopilotEditDialog(AutopilotDevice record)
    {
        var services = AppServices.Current;
        ViewModel = new AutopilotEditViewModel(record, services.Session.AutopilotUpdates, services.Settings, services.Session.SessionExpired);
        People = new SearchViewModel(services.Session.Lookup, services.Session.Directory, services.Session.SessionExpired, includeDevices: false, includeGroups: false);
        InitializeComponent();
        Opened += (_, _) => TagBox.Focus(FocusState.Programmatic);
    }

    public AutopilotEditViewModel ViewModel { get; }

    /// <summary>The person picker (ports <c>UserPickerView</c>): people search only.</summary>
    public SearchViewModel People { get; }

    /// <summary>True once Graph accepted the changes.</summary>
    public bool Saved { get; private set; }

    private async void Save_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            Saved = await ViewModel.SaveAsync();
            // Stay open to show what went wrong.
            args.Cancel = !Saved;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void SuggestedTag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag }) ViewModel.GroupTagText = tag;
    }

    private void PickUser_Click(object sender, RoutedEventArgs e)
    {
        Picker.Visibility = Picker.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        if (Picker.Visibility == Visibility.Visible) PersonSearch.Focus(FocusState.Programmatic);
    }

    private void ToggleRemove_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleRemove();

    private void PersonSearch_TextChanged(object sender, TextChangedEventArgs e) => People.Query = PersonSearch.Text;

    private void Person_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not DirectoryUser person) return;
        ViewModel.AssignUser(person);
        Picker.Visibility = Visibility.Collapsed;
        PickUserButton.Focus(FocusState.Programmatic);
    }
}
