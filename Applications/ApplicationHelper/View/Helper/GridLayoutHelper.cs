using Avalonia.Controls;

namespace ApplicationHelper.View.Helper;

public static class GridLayoutHelper
{
    /// <summary>
    /// Grid に子要素を追加する際に、行・列・行スパン・列スパンを指定して追加する拡張メソッド
    /// </summary>
    /// <param name="grid"></param>
    /// <param name="child"></param>
    /// <param name="row"></param>
    /// <param name="column"></param>
    /// <param name="rowSpan"></param>
    /// <param name="columnSpan"></param>
    /// <returns></returns>
    public static Control AddChildAt(
        this Grid grid,
        Control child,
        int row,
        int column,
        int rowSpan = 1,
        int columnSpan = 1)
    {
        Grid.SetRow(child, row);
        Grid.SetColumn(child, column);
        Grid.SetRowSpan(child, rowSpan);
        Grid.SetColumnSpan(child, columnSpan);
        grid.Children.Add(child);
        return child;
    }

    public static Grid WithChild(
        this Grid grid,
        Control child,
        int row,
        int column,
        int rowSpan = 1,
        int columnSpan = 1)
    {
        grid.AddChildAt(child, row, column, rowSpan, columnSpan);
        return grid;
    }
}
