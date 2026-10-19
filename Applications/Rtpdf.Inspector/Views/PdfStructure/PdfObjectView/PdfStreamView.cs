using Avalonia.Controls;
using Rtpdf.Inspector.ViewModels.PdfStructure;

namespace Rtpdf.Inspector.Views.PdfStructure.PdfObjectView;

internal class PdfStreamView(PdfStreamViewModel viewModel) : PdfObjectView(viewModel)
{
    private readonly PdfStreamViewModel _viewModel = viewModel;

    public override Control Render()
    {
        return new PdfDictionaryView(_viewModel.Dictionary).Render();
    }
}
