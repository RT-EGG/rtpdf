using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Collections;

namespace ApplicationHelper.View;

public class ColumnedListBoxBase : ContentControl
{
    public static readonly StyledProperty<IBrush?> HeaderSeparatorBrushProperty =
        AvaloniaProperty.Register<ColumnedListBoxBase, IBrush?>(
            nameof(HeaderSeparatorBrush));

    public static readonly StyledProperty<double> HeaderSeparatorThicknessProperty =
        AvaloniaProperty.Register<ColumnedListBoxBase, double>(
            nameof(HeaderSeparatorThickness),
            2.0);

    public static readonly StyledProperty<double> HeaderResizeHandleWidthProperty =
        AvaloniaProperty.Register<ColumnedListBoxBase, double>(
            nameof(HeaderResizeHandleWidth),
            8.0);

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<ColumnedListBoxBase, double>(
            nameof(RowHeight),
            8.0);

    public static readonly StyledProperty<SelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<ColumnedListBoxBase, SelectionMode>(
            nameof(SelectionMode),
            SelectionMode.Single);

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<ColumnedListBoxBase, object?>(
            nameof(SelectedItem));

    public static readonly StyledProperty<IList?> SelectedItemsProperty =
        AvaloniaProperty.Register<ColumnedListBoxBase, IList?>(
            nameof(SelectedItems));

    public IBrush? HeaderSeparatorBrush
    {
        get => GetValue(HeaderSeparatorBrushProperty);
        set => SetValue(HeaderSeparatorBrushProperty, value);
    }

    public double HeaderSeparatorThickness
    {
        get => GetValue(HeaderSeparatorThicknessProperty);
        set => SetValue(HeaderSeparatorThicknessProperty, value);
    }

    public double HeaderResizeHandleWidth
    {
        get => GetValue(HeaderResizeHandleWidthProperty);
        set => SetValue(HeaderResizeHandleWidthProperty, value);
    }

    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public SelectionMode SelectionMode
    {
        get => GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public IList? SelectedItems
    {
        get => GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }
}
