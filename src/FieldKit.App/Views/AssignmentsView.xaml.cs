using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Views;

/// <summary>A device's or person's groups and assignments (ports <c>AssignmentsView</c>). Groups open the group page.</summary>
public sealed partial class AssignmentsView : UserControl
{
    public AssignmentsView()
    {
        InitializeComponent();
    }

    public AssignmentsViewModel? ViewModel { get; private set; }

    /// <summary>Raised when a group is picked; the hosting page navigates.</summary>
    public event EventHandler<DirectoryGroup>? GroupInvoked;

    public void Show(AssignmentsViewModel model)
    {
        ViewModel = model;
        Bindings.Update();
    }

    private void Group_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DirectoryGroup group) GroupInvoked?.Invoke(this, group);
    }
}
