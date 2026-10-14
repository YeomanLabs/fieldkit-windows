using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace FieldKit.App.Controls;

/// <summary>A label and value with a copy button, for pasting into the Entra portal (ports <c>CopyableValueRow</c>).</summary>
public sealed partial class CopyableValue : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(CopyableValue), new PropertyMetadata("", (d, _) => ((CopyableValue)d).UpdateName()));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(CopyableValue), new PropertyMetadata(""));

    private readonly DispatcherTimer _reset = new() { Interval = TimeSpan.FromSeconds(2) };

    public CopyableValue()
    {
        InitializeComponent();
        _reset.Tick += (_, _) =>
        {
            _reset.Stop();
            CopyIcon.Glyph = "";
        };
        UpdateName();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        // Setup values are identifiers, not secrets, so the normal clipboard is right here.
        var package = new DataPackage();
        package.SetText(Value);
        Clipboard.SetContent(package);
        CopyIcon.Glyph = "";
        _reset.Start();
    }

    private void UpdateName()
    {
        AutomationProperties.SetName(CopyButton, $"Copy {Label}");
        ToolTipService.SetToolTip(CopyButton, $"Copy {Label}");
    }
}
