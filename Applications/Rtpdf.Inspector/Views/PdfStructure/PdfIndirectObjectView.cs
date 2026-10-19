using ApplicationHelper;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Rtpdf.Inspector.ViewModels.PdfStructure;

namespace Rtpdf.Inspector.Views.PdfStructure;

[HotReloadTrigger]
internal class PdfIndirectObjectView(PdfIndirectObjectViewModel viewModel)
{
    private PdfIndirectObjectViewModel _viewModel = viewModel;

    public Control Render()
    {
        return new ScrollViewer
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = PdfObjectView.PdfObjectView.CreateView(_viewModel.ObjectViewModel).Render(),
        };
    }

    private TextBlock CreateSimpleTextObjectView(PdfObjectViewModel viewModel)
    {
        var textBlock = new TextBlock
        {
            Text = viewModel.AsString(),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        return textBlock;
    }
}
