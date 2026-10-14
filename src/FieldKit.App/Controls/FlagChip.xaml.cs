using FieldKit.Core.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Controls;

/// <summary>One flag as a coloured chip: icon and colour by severity, read by Narrator as "Warning: Stale".</summary>
public sealed partial class FlagChip : UserControl
{
    public static readonly DependencyProperty FlagProperty = DependencyProperty.Register(
        nameof(Flag), typeof(Flag), typeof(FlagChip), new PropertyMetadata(null, (d, _) => ((FlagChip)d).Update()));

    public FlagChip()
    {
        InitializeComponent();
    }

    public Flag? Flag
    {
        get => (Flag?)GetValue(FlagProperty);
        set => SetValue(FlagProperty, value);
    }

    private void Update()
    {
        if (Flag is not { } flag) return;
        Label.Text = flag.Kind.Title();
        AutomationProperties.SetName(this, $"{flag.Severity.Title()}: {flag.Kind.Title()}");
        VisualStateManager.GoToState(this, flag.Severity.ToString(), false);
    }
}
