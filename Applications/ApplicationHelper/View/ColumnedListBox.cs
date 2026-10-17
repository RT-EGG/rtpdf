using ApplicationHelper.View.Helper;
using ApplicationHelper.ViewModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace ApplicationHelper.View;

[HotReloadTrigger]
public class ColumnedListBox<VM, Column, Item> : ColumnedListBoxBase
    where VM : ColumnedListBoxViewModel<Column, Item>
    where Column : ColumnedListBoxColumnViewModel
{
    private new object? Content => base.Content as ContentControl;
    private VM _viewModel;

    public Func<VM, Column, INameScope, Control?> CreateHeaderColumnControl { get; init; }
    public Func<VM, Column, int, Item, Control?> CreateCellControl { get; set; }

    public ColumnedListBox(VM viewModel)
    {
        _viewModel = viewModel;
        CreateHeaderColumnControl = CreateDefaultHeaderColumn;
        CreateCellControl = CreateDefaultCellControl;

        base.Content = CreateContent();
    }

    private Grid CreateContent()
    {
        var header = CreateHeader();
        var listBody = CreateListBody();
        BindHorizontalOffset(header, listBody);

        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowDefinitions = [
                new (GridLength.Auto),
                new (GridLength.Star),
            ],
            ColumnDefinitions = [
                new (GridLength.Star),
            ],
        }.WithChild(header, 0, 0)
         .WithChild(listBody, 1, 0);

        return grid;
    }

    private ScrollViewer CreateHeader()
    {
        var items = new ItemsControl
        {
            ItemsPanel = new FuncTemplate<Panel?>(() =>
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                }
            ),
            ItemTemplate = new FuncDataTemplate<Column>(CreateHeaderColumn, false),
            [!ItemsControl.ItemsSourceProperty] = CompiledBinding.Create<VM, IEnumerable<Column>>(
                source: _viewModel,
                expression: vm => vm.Columns
            ),
            [!ItemsControl.HeightProperty] = CompiledBinding.Create<VM, double>(
                source: _viewModel,
                expression: vm => vm.Header!.Height,
                mode: BindingMode.OneWay,
                fallbackValue: 0.0
            ),
        };

        var scrollViewer = new ScrollViewer
        {
            Content = items,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            AllowAutoHide = true,
            [!ScrollViewer.IsVisibleProperty] = CompiledBinding.Create<VM, bool>(
                source: _viewModel,
                expression: vm => vm.IsHeaderVisible,
                mode: BindingMode.OneWay
            ),
        };

        return scrollViewer;
    }

    public static TextBlock CreateDefaultHeaderColumn(VM viewModel, Column column, INameScope _)
    {
        return new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextAlignment = TextAlignment.Center,
            Text = column.ID,
            Margin = new Thickness(4, 0, 4, 0),
        };
    }

    private Control? CreateHeaderColumn(Column column, INameScope nameScope)
    {
        var control = CreateHeaderColumnControl(_viewModel, column, nameScope);
        if (control is null)
        {
            return control;
        }

        var controlContainer = new ContentControl
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = control,
            [!ContentControl.WidthProperty] = CompiledBinding.Create<Column, double>(
                source: column,
                expression: c => c.Width,
                mode: BindingMode.OneWay
            ),
        };

        var resizableCursor = column.Resizeable
            ? new Cursor(StandardCursorType.SizeWestEast)
            : null;

        var thumb = new Thumb
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = resizableCursor,
            Background = Brushes.Transparent,
            Template = new FuncControlTemplate<Thumb>((thumb, _) =>
            {
                return new Border
                {
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    [!Border.WidthProperty] = this[!HeaderSeparatorThicknessProperty],
                    [!Border.BackgroundProperty] = this[!HeaderSeparatorBrushProperty],
                };
            }),
            [!Thumb.WidthProperty] = this[!HeaderResizeHandleWidthProperty],
            [!Thumb.HeightProperty] = CompiledBinding.Create<VM, double>(
                source: _viewModel,
                expression: vm => vm.Header!.Height,
                mode: BindingMode.OneWay,
                fallbackValue: 0.00
            ),
        };

        if (column.Resizeable)
        {
            thumb.DragDelta += (_, delta) =>
            {
                var newWidth = column.Width + delta.Vector.X;
                if (newWidth < column.MinWidth)
                {
                    newWidth = column.MinWidth;
                }
                column.Width = newWidth;
            };
        }

        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Left,
            Orientation = Orientation.Horizontal,
            Children =
            {
                controlContainer,
                thumb,
            },
        };
        return panel;
    }

    private ListBox CreateListBody()
    {
        var listBox = new ListBox
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = null,
            ItemTemplate = new FuncDataTemplate<Item>(CreateRowControl, false),
            [!ListBox.ItemsSourceProperty] = CompiledBinding.Create<VM, IEnumerable<Item>>(
                source: _viewModel,
                expression: vm => vm.Items
            ),
            [ScrollViewer.HorizontalScrollBarVisibilityProperty] = ScrollBarVisibility.Auto,
            [ScrollViewer.VerticalScrollBarVisibilityProperty] = ScrollBarVisibility.Auto,
            [!ListBox.SelectionModeProperty] = this[!SelectionModeProperty]
        }.WithStyle(new Style(selector => selector.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(ListBoxItem.PaddingProperty, new Thickness(0)),
                new Setter(ListBoxItem.MarginProperty, new Thickness(0)),
                new Setter(ListBoxItem.MinHeightProperty, 0.0),
                new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Left),
                new Setter(ListBoxItem.VerticalContentAlignmentProperty, VerticalAlignment.Stretch),
            },
        });

        return listBox;
    }

    private Control CreateRowControl(Item item, INameScope _)
    {
        var itemsControl = new ItemsControl
        {
            ItemsPanel = new FuncTemplate<Panel?>(() =>
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                }
            ),
            Padding = new(0),
            [!ItemsControl.HeightProperty] = this[!RowHeightProperty],
            ItemsSource = _viewModel.Columns,
            ItemTemplate = new FuncDataTemplate<Column>((column, scope) =>
            {
                var columnIndex = _viewModel.Columns.IndexOf(column);
                var cellControl =
                    CreateCellControl(_viewModel, column, columnIndex, item);

                if (cellControl is null)
                {
                    return cellControl;
                }

                var cellContainer = new ContentControl
                {
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Content = cellControl,
                    Margin = new(),
                    [!ContentControl.WidthProperty] = CompiledBinding.Create<Column, double>(
                        source: column,
                        expression: c => c.Width,
                        mode: BindingMode.OneWay
                    )
                };

                var blank = new Panel
                {
                    [!Panel.WidthProperty] = this[!HeaderResizeHandleWidthProperty],
                };

                var container = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new(),
                    Spacing = 0.0,
                };
                container.Children.Add(cellContainer);
                container.Children.Add(blank);

                return container;
            }, false),
        };

        return itemsControl;
    }

    public static TextBlock CreateDefaultCellControl(VM viewModel, Column column, int columnIndex, Item item)
    {
        return new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextAlignment = TextAlignment.Right,
            Text = item?.ToString(),
            Margin = new Thickness(0, 0, 0, 0),
        };
    }

    private static void BindHorizontalOffset(ScrollViewer header, ListBox listBox)
    {
        listBox.TemplateApplied += (_, _) =>
        {
            if (listBox.Scroll is not ScrollViewer body)
            {
                return;
            }

            header[!ScrollViewer.OffsetProperty] =
                CompiledBinding.Create<ScrollViewer, Vector>(
                    source: body,
                    expression: scroll => scroll.Offset,
                    mode: BindingMode.OneWay,
                    converter: new FuncValueConverter<Vector, Vector>(
                        offset => new Vector(offset.X, 0)));
        };
    }
}
