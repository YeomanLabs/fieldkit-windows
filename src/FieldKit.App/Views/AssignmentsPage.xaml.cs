using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FieldKit.App.Views;

/// <summary>Groups and assignments for the <see cref="AssignmentSubject"/> passed in.</summary>
public sealed partial class AssignmentsPage : Page
{
    private AssignmentsViewModel? _model;

    public AssignmentsPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        var subject = (AssignmentSubject)e.Parameter;
        var session = AppServices.Current.Session;
        SubjectText.Text = subject.Title;
        _model = new AssignmentsViewModel(subject, session.Directory, session.AssignmentCatalog, session.SessionExpired);
        Assignments.Show(_model);
        await _model.LoadAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) => _model?.Cancel();

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_model is not null) await _model.RefreshAsync();
    }

    private void Assignments_GroupInvoked(object? sender, DirectoryGroup group) => Frame.Navigate(typeof(GroupPage), group);
}
