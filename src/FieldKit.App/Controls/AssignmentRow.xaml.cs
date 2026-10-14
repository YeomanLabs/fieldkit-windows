using FieldKit.Core.Domain;
using Windows.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Controls;

/// <summary>
/// One app, profile, policy or script and why it applies (ports <c>AssignmentRow.swift</c>):
/// struck through when an exclusion wins.
/// </summary>
public sealed partial class AssignmentRow : UserControl
{
    public static readonly DependencyProperty AssignmentProperty = DependencyProperty.Register(
        nameof(Assignment), typeof(ResolvedAssignment), typeof(AssignmentRow), new PropertyMetadata(null, (d, _) => ((AssignmentRow)d).Update()));

    public AssignmentRow()
    {
        InitializeComponent();
    }

    public ResolvedAssignment? Assignment
    {
        get => (ResolvedAssignment?)GetValue(AssignmentProperty);
        set => SetValue(AssignmentProperty, value);
    }

    private void Update()
    {
        if (Assignment is not { } assignment) return;
        NameText.Text = assignment.Item.Name;
        NameText.TextDecorations = assignment.IsExcluded ? TextDecorations.Strikethrough : TextDecorations.None;

        Intents.Children.Clear();
        foreach (var intent in assignment.Intents)
        {
            Intents.Children.Add(new Border
            {
                Style = (Style)Application.Current.Resources["IntentChipStyle"],
                Child = new TextBlock { Text = intent, Style = (Style)Application.Current.Resources["IntentChipTextStyle"] },
            });
        }

        Show(DetailText, assignment.Item.Detail);
        var via = assignment.IncludedVia.Count > 0 ? $"Via {string.Join(", ", assignment.IncludedVia)}" : "";
        Show(ViaText, via);
        var excluded = assignment.IsExcluded ? $"Excluded via {string.Join(", ", assignment.ExcludedVia)}" : "";
        ExcludedText.Text = excluded;
        ExcludedPanel.Visibility = assignment.IsExcluded ? Visibility.Visible : Visibility.Collapsed;

        var parts = new[] { assignment.Item.Name, string.Join(", ", assignment.Intents), assignment.Item.Detail, via, excluded };
        AutomationProperties.SetName(this, string.Join(", ", parts.Where(part => part.Length > 0)));
    }

    private static void Show(TextBlock text, string value)
    {
        text.Text = value;
        text.Visibility = value.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
