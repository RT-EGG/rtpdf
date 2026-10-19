using ApplicationHelper.Utility;
using ApplicationHelper.ViewModel;
using Rtpdf.Core.Objects;
using Rtpdf.Inspector.Models;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal class PdfTrailerViewModel : ViewModelBase
{
    private readonly PdfInspectionSession _model;
    private readonly PdfDictionary _trailer;

    public PdfDocumentInfoViewModel? DocumentInfo { get; }
    public string? ID { get; }

    public PdfTrailerViewModel(PdfInspectionSession model)
    {
        _model = model;
        _trailer = _model.CrossReferences.Trailer;

        if (_trailer.TryGetValueAs<PdfDictionary>("Info", _model.ObjectStore, true, out var infoDict))
        {
            DocumentInfo = new PdfDocumentInfoViewModel(_model, infoDict);
        }

        if (_trailer.TryGetValueAs<PdfArray>("ID", _model.ObjectStore, true, out var idsArray))
        {
            if (idsArray.Count == 2)
            {
                if (idsArray[0] is PdfString id0 && idsArray[1] is PdfString id1)
                {
                    ID = $"{id0}, {id1}";
                }
            }
        }
    }
}
