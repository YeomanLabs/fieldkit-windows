using FieldKit.Core.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Controls;

/// <summary>A group in search results and membership lists (ports <c>GroupRow.swift</c>).</summary>
public sealed partial class GroupRow : UserControl
{
    public static readonly DependencyProperty GroupProperty = DependencyProperty.Register(
        nameof(Group), typeof(DirectoryGroup), typeof(GroupRow), new PropertyMetadata(null, (d, _) => ((GroupRow)d).Update()));

    public GroupRow()
    {
        InitializeComponent();
    }

    public DirectoryGroup? Group
    {
        get => (DirectoryGroup?)GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    private void Update()
    {
        if (Group is not { } group) return;
        NameText.Text = group.DisplayName;
        KindText.Text = group.KindTitle;
        // People for assigned groups; the "sync" people glyph for dynamic ones.
        Icon.Glyph = group.IsDynamic ? "" : "";
        AutomationProperties.SetName(this, $"{group.DisplayName}, {group.KindTitle}");
    }
}
