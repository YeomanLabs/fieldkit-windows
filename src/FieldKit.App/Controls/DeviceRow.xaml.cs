using FieldKit.App.Views;
using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Controls;

/// <summary>A device in search results or a person's devices (ports <c>DeviceRow.swift</c>).</summary>
public sealed partial class DeviceRow : UserControl
{
    public static readonly DependencyProperty DeviceProperty = DependencyProperty.Register(
        nameof(Device), typeof(IntuneDevice), typeof(DeviceRow), new PropertyMetadata(null, (d, _) => ((DeviceRow)d).Update()));

    public DeviceRow()
    {
        InitializeComponent();
    }

    public IntuneDevice? Device
    {
        get => (IntuneDevice?)GetValue(DeviceProperty);
        set => SetValue(DeviceProperty, value);
    }

    private void Update()
    {
        if (Device is not { } device) return;
        var opens = DeviceText.LookupSerial(device) is not null;
        PlatformIcon.Glyph = Ui.DeviceGlyph(device.OperatingSystem);
        NameText.Text = DeviceText.Name(device);
        SubtitleText.Text = DeviceText.ListSubtitle(device);
        SyncedText.Text = DeviceText.Synced(device);
        var compliant = device.ComplianceState.Kind == ComplianceStateKind.Compliant;
        ComplianceIcon.Visibility = compliant ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(ComplianceIcon, device.ComplianceState.DisplayName);
        // Without a usable serial the device page can't look the device up.
        Row.Opacity = opens ? 1 : 0.55;
        var summary = $"{NameText.Text}, {SubtitleText.Text}, {SyncedText.Text}";
        if (!compliant) summary += $", {device.ComplianceState.DisplayName}";
        if (!opens) summary += ", no serial to open";
        AutomationProperties.SetName(this, summary);
    }
}
