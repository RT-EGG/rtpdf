using Rtpdf.Core.Objects;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal class PdfStreamViewModel : PdfCollapsableObjectViewModel
{
    private readonly PdfStream _model;
    public PdfDictionaryViewModel Dictionary { get; }

    public int DataLength => _model.EncodedLength;

    public PdfStreamViewModel(PdfStream model) : base(model)
    {
        _model = model;
        Dictionary = new PdfDictionaryViewModel(_model.Dictionary);
    }
}
