using ApplicationHelper;
using Avalonia.Controls;
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
        var list = new ColumnedListBox(_viewModel.ListBox)
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
                    "ObjectNo" => item.ObjectNumber,
                    "GenNo" => item.GenerationNumber,
                    "Kind" => item.Kind,
                    "Offset/NextFree" => item.OffsetOrNextFree,
                    _ => textBlock.Text,
                };
                textBlock.FontFamily = new FontFamily("avares://Rtpdf.Inspector/Assets/Fonts#Cascadia Mono");
                return textBlock;
            },
            RowHeight = 24.0,
            SelectionMode = SelectionMode.Single,
        };

        return list;
    }
}
