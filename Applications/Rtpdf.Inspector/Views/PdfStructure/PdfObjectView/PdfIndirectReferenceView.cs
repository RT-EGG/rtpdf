using Avalonia.Controls;
using Rtpdf.Inspector.ViewModels.PdfStructure;

namespace Rtpdf.Inspector.Views.PdfStructure.PdfObjectView;

internal class PdfIndirectReferenceView(PdfIndirectReferenceViewModel viewModel) : PdfObjectView(viewModel)
{
    private readonly PdfIndirectReferenceViewModel _viewModel = viewModel;

    public override Control Render()
    {
        return CreateView(_viewModel.ReferencedObject).Render();
    }
}
