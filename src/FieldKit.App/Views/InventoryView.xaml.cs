using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Views;

/// <summary>The device page's hardware, storage and apps tab: two columns when there's room.</summary>
public sealed partial class InventoryView : UserControl
{
    private const double TwoColumnWidth = 880;

    public InventoryView()
    {
        InitializeComponent();
    }

    public DeviceInventoryViewModel? ViewModel { get; private set; }

    public void Show(DeviceInventoryViewModel model)
    {
        ViewModel = model;
        Bindings.Update();
    }

    private void Columns_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= TwoColumnWidth;
        SecondColumn.Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Columns.ColumnSpacing = wide ? 16 : 0;
        Grid.SetColumn(RightColumn, wide ? 1 : 0);
        Grid.SetRow(RightColumn, wide ? 0 : 1);
    }
}
