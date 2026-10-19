using Rtpdf.Core.Objects;
using Rtpdf.Core.Storage;
using System;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal class PdfIndirectReferenceViewModel : PdfCollapsableObjectViewModel
{
    private readonly PdfIndirectReference _model;
    private Lazy<PdfObjectViewModel> _lazyReferencedObject;
    public PdfObjectViewModel ReferencedObject => _lazyReferencedObject.Value;

    public string Reference => $"[{_model.ObjectId.ObjectNumber} {_model.ObjectId.GenerationNumber} R] ({ReferencedObject.GetPdfObjectTypeString()})";

    public PdfIndirectReferenceViewModel(PdfObjectStore objectStore, PdfIndirectReference model) : base(model)
    {
        _model = model;
        _lazyReferencedObject = new Lazy<PdfObjectViewModel>(() => PdfObjectViewModel.Create(
            objectStore,
            objectStore.GetIndirectObject(model.ObjectId).Value
        ));
    }
}
