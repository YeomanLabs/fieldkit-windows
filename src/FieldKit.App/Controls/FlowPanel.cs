using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace FieldKit.App.Controls;

/// <summary>Lays children out left to right, wrapping onto new lines (ports <c>FlowLayout.swift</c>).</summary>
public sealed class FlowPanel : Panel
{
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(FlowPanel), new PropertyMetadata(8.0, (d, _) => ((FlowPanel)d).InvalidateMeasure()));

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        }
        var rows = Arrange(availableSize.Width);
        var width = rows.Count == 0 ? 0 : rows.Max(row => row.Width);
        var height = rows.Count == 0 ? 0 : rows[^1].Y + rows[^1].Height;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var row in Arrange(finalSize.Width))
        {
            var x = 0.0;
            foreach (var child in row.Children)
            {
                child.Arrange(new Rect(x, row.Y, child.DesiredSize.Width, child.DesiredSize.Height));
                x += child.DesiredSize.Width + Spacing;
            }
        }
        return finalSize;
    }

    private List<Row> Arrange(double maxWidth)
    {
        var rows = new List<Row>();
        var current = new Row(0);
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            var widthWithChild = current.Children.Count == 0 ? size.Width : current.Width + Spacing + size.Width;
            if (widthWithChild > maxWidth && current.Children.Count > 0)
            {
                rows.Add(current);
                current = new Row(current.Y + current.Height + Spacing);
                widthWithChild = size.Width;
            }
            current.Width = widthWithChild;
            current.Height = Math.Max(current.Height, size.Height);
            current.Children.Add(child);
        }
        if (current.Children.Count > 0) rows.Add(current);
        return rows;
    }

    private sealed class Row(double y)
    {
        public double Y { get; } = y;

        public double Width { get; set; }

        public double Height { get; set; }

        public List<UIElement> Children { get; } = [];
    }
}
