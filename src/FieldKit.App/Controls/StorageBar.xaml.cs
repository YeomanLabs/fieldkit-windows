using System.Globalization;
using FieldKit.Core.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Controls;

/// <summary>Used-space bar, green → amber → red as free space runs out (ports <c>StorageBar.swift</c>).</summary>
public sealed partial class StorageBar : UserControl
{
    public static readonly DependencyProperty UsageProperty = DependencyProperty.Register(
        nameof(Usage), typeof(StorageUsage), typeof(StorageBar), new PropertyMetadata(null, (d, _) => ((StorageBar)d).Update()));

    public StorageBar()
    {
        InitializeComponent();
    }

    public StorageUsage? Usage
    {
        get => (StorageUsage?)GetValue(UsageProperty);
        set => SetValue(UsageProperty, value);
    }

    private void Update()
    {
        if (Usage is not { } usage)
        {
            Bar.Value = 0;
            Summary.Text = "";
            Free.Text = "";
            return;
        }
        Bar.Value = usage.UsedShare * 100;
        Summary.Text = usage.Summary;
        var free = (int)Math.Round(usage.FreeShare * 100);
        Free.Text = string.Create(CultureInfo.CurrentCulture, $"{free}% free");
        AutomationProperties.SetName(this, $"Storage: {usage.Summary}, {free} percent free");
        VisualStateManager.GoToState(this, usage.Level.ToString(), false);
    }
}
