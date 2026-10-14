using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Controls;

/// <summary>Text coloured by its <see cref="Tone"/>; empty text shows as a dimmed dash.</summary>
public sealed partial class ToneText : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(ToneText), new PropertyMetadata("", (d, _) => ((ToneText)d).Update()));

    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(Tone), typeof(ToneText), new PropertyMetadata(Tone.Normal, (d, _) => ((ToneText)d).Update()));

    public static readonly DependencyProperty IsSelectableProperty = DependencyProperty.Register(
        nameof(IsSelectable), typeof(bool), typeof(ToneText), new PropertyMetadata(true));

    public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
        nameof(TextAlignment), typeof(TextAlignment), typeof(ToneText), new PropertyMetadata(TextAlignment.Left));

    public ToneText()
    {
        InitializeComponent();
        Update();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Tone Tone
    {
        get => (Tone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public bool IsSelectable
    {
        get => (bool)GetValue(IsSelectableProperty);
        set => SetValue(IsSelectableProperty, value);
    }

    public TextAlignment TextAlignment
    {
        get => (TextAlignment)GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    private void Update()
    {
        var empty = string.IsNullOrEmpty(Text);
        Label.Text = empty ? "—" : Text;
        AutomationProperties.SetName(Label, empty ? "Not set" : Text);
        VisualStateManager.GoToState(this, empty ? "Empty" : Tone.ToString(), false);
    }
}
