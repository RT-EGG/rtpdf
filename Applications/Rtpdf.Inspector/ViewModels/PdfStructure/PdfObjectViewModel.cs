using ApplicationHelper.ViewModel;
using Rtpdf.Core.Objects;
using System;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal class PdfObjectViewModel : ViewModelBase
{
    public PdfObject Model { get; }
    public PdfObjectViewModel(PdfObject model)
    {
        Model = model;
    }

    public string AsString()
    {
        return Model?.ToString() ?? "???";
    }

    public static PdfObjectViewModel Create(PdfObject source)
    {
        return source.Type switch
        {
            PdfObjectType.Null or PdfObjectType.Boolean
                or PdfObjectType.Integer or PdfObjectType.Real
                or PdfObjectType.Name => new PdfObjectViewModel(source),
            PdfObjectType.String => new PdfStringViewModel((PdfString)source),
            PdfObjectType.Array => new PdfArrayViewModel((PdfArray)source),
            PdfObjectType.Dictionary => new PdfDictionaryViewModel((PdfDictionary)source),
            PdfObjectType.Stream => new PdfStreamViewModel((PdfStream)source),
            PdfObjectType.IndirectReference => new PdfIndirectReferenceViewModel((PdfIndirectReference)source),
            _ => throw new NotImplementedException(),
        };
    }
}
