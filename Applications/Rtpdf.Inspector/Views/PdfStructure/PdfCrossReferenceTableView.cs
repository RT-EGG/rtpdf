using ApplicationHelper;
using ApplicationHelper.View.Helper;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Rtpdf.Inspector.ViewModels.PdfStructure;
using ColumnedListBox = ApplicationHelper.View.ColumnedListBox<
    Rtpdf.Inspector.ViewModels.PdfStructure.PdfCrossReferenceTableViewModel.ListBoxViewModel,
    Rtpdf.Inspector.ViewModels.PdfStructure.PdfCrossReferenceTableViewModel.ColumnViewModel,
    Rtpdf.Inspector.ViewModels.PdfStructure.PdfCrossReferenceTableViewModel.ItemViewModel>;

namespace Rtpdf.Inspector.Views.PdfStructure;

[HotReloadTrigger]
internal class PdfCrossReferenceTableView(PdfCrossReferenceTableViewModel viewModel)
{
    private readonly PdfCrossReferenceTableViewModel _viewModel = viewModel;

    public Control Render()
    {
        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowDefinitions = [
                new(GridLength.Star),
            ],
            ColumnDefinitions = [
                new(400.0, GridUnitType.Pixel),
                new(4.0, GridUnitType.Pixel),
                new(GridLength.Star) { MinWidth = 200.0, },
            ]
        }.WithChild(new ColumnedListBox(_viewModel.ListBox)
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            CreateHeaderColumnControl = (vm, column, scope) =>
            {
                var textBlock = ColumnedListBox.CreateDefaultHeaderColumn(vm, column, scope);
                textBlock.TextAlignment = TextAlignment.Left;

                return textBlock;
            },
            CreateCellControl = (vm, column, columnIndex, item) =>
            {
                var textBlock = ColumnedListBox.CreateDefaultCellControl(vm, column, columnIndex, item);
                textBlock.TextAlignment = TextAlignment.Right;
                textBlock.Text = column.ID switch
                {
                    "ObjectNo" => item.ObjectNumberText,
                    "GenNo" => item.GenerationNumberText,
                    "Kind" => item.Kind,
                    "Offset/NextFree" => item.OffsetOrNextFreeText,
                    _ => textBlock.Text,
                };
                textBlock.FontFamily = new FontFamily("avares://Rtpdf.Inspector/Assets/Fonts#Cascadia Mono");
                return textBlock;
            },
            RowHeight = 24.0,
            SelectionMode = SelectionMode.Single,
            [!ColumnedListBox.SelectedItemProperty] = CompiledBinding.Create<PdfCrossReferenceTableViewModel, object?>(
                    source: _viewModel,
                    expression: vm => vm.SelectedItem,
                    mode: BindingMode.TwoWay
                )
        }, 0, 0)
        .WithChild(new GridSplitter
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        }, 0, 1)
        .WithChild(new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            [!ContentControl.ContentProperty] = CompiledBinding.Create<PdfCrossReferenceTableViewModel, PdfIndirectObjectViewModel?>(
                source: _viewModel,
                expression: vm => vm.SelectedIndirectObjectViewModel,
                converter: new FuncValueConverter<PdfIndirectObjectViewModel?, Control?>(
                    obj => obj is null ? null : new PdfIndirectObjectView(obj).Render()
                )
            )
        }, 0, 2);

        return grid;
    }
}
