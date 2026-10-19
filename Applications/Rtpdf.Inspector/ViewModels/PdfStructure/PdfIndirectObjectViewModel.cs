using ApplicationHelper.ViewModel;
using Rtpdf.Core.Objects;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal class PdfIndirectObjectViewModel : ViewModelBase
{
    private readonly PdfIndirectObject _model;
    public PdfObjectViewModel ObjectViewModel;

    public PdfIndirectObjectViewModel(PdfIndirectObject model)
    {
        _model = model;

        ObjectViewModel = PdfObjectViewModel.Create(model.Value);
    }
}
