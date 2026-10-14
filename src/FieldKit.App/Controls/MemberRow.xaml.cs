using FieldKit.Core.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Controls;

/// <summary>A group member: person, device, nested group or other object (ports the rows in <c>GroupMembersView</c>).</summary>
public sealed partial class MemberRow : UserControl
{
    public static readonly DependencyProperty MemberProperty = DependencyProperty.Register(
        nameof(Member), typeof(GroupMember), typeof(MemberRow), new PropertyMetadata(null, (d, _) => ((MemberRow)d).Update()));

    public static readonly DependencyProperty IsRemovingProperty = DependencyProperty.Register(
        nameof(IsRemoving), typeof(bool), typeof(MemberRow), new PropertyMetadata(false, (d, _) => ((MemberRow)d).Update()));

    public MemberRow()
    {
        InitializeComponent();
    }

    public GroupMember? Member
    {
        get => (GroupMember?)GetValue(MemberProperty);
        set => SetValue(MemberProperty, value);
    }

    /// <summary>Dims the row while it's being removed.</summary>
    public bool IsRemoving
    {
        get => (bool)GetValue(IsRemovingProperty);
        set => SetValue(IsRemovingProperty, value);
    }

    private void Update()
    {
        if (Member is not { } member) return;
        var (glyph, kind, detail) = member.Kind switch
        {
            GroupMemberKind.User user => ("", "Person", user.UserPrincipalName),
            GroupMemberKind.Device device => ("", "Device", device.OperatingSystem),
            GroupMemberKind.Group => ("", "Group", ""),
            _ => ("", "Other object", ""),
        };
        Icon.Glyph = glyph;
        NameText.Text = member.DisplayName;
        DetailText.Text = detail;
        DetailText.Visibility = detail.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        Opacity = IsRemoving ? 0.4 : 1;
        AutomationProperties.SetName(this, string.Join(", ", new[] { member.DisplayName, kind, detail }.Where(part => part.Length > 0)));
    }
}
