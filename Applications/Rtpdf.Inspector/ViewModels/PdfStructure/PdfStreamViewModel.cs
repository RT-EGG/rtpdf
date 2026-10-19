using Rtpdf.Core.Objects;
using Rtpdf.Core.Storage;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal class PdfStreamViewModel : PdfCollapsableObjectViewModel
{
    private readonly PdfStream _model;
    public PdfDictionaryViewModel Dictionary { get; }

    public int DataLength => _model.EncodedLength;

    public PdfStreamViewModel(PdfObjectStore objectStore, PdfStream model) : base(model)
    {
        _model = model;
        Dictionary = new PdfDictionaryViewModel(objectStore, _model.Dictionary);
    }
}
