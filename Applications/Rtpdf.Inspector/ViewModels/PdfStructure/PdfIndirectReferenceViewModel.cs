using Rtpdf.Core.Objects;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal class PdfIndirectReferenceViewModel : PdfObjectViewModel
{
    private readonly PdfIndirectReference _model;
    public PdfIndirectReferenceViewModel(PdfIndirectReference model) : base(model)
    {
        _model = model;
    }
}
