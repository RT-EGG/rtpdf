using ApplicationHelper.ViewModel;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Storage;
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

    public static PdfObjectViewModel Create(PdfObjectStore objectStore, PdfObject source)
    {
        return source.Type switch
        {
            PdfObjectType.Null or PdfObjectType.Boolean
                or PdfObjectType.Integer or PdfObjectType.Real
                or PdfObjectType.Name => new PdfObjectViewModel(source),
            PdfObjectType.String => new PdfStringViewModel((PdfString)source),
            PdfObjectType.Array => new PdfArrayViewModel(objectStore, (PdfArray)source),
            PdfObjectType.Dictionary => new PdfDictionaryViewModel(objectStore, (PdfDictionary)source),
            PdfObjectType.Stream => new PdfStreamViewModel(objectStore, (PdfStream)source),
            PdfObjectType.IndirectReference => new PdfIndirectReferenceViewModel(objectStore, (PdfIndirectReference)source),
            _ => throw new NotImplementedException(),
        };
    }

    public string GetPdfObjectTypeString()
    {
        return Model.Type switch
        {
            PdfObjectType.Null => "null",
            PdfObjectType.Boolean => "boolean",
            PdfObjectType.Integer => "integer",
            PdfObjectType.Real => "real",
            PdfObjectType.Name => "name",
            PdfObjectType.String => "string",
            PdfObjectType.Array => "array",
            PdfObjectType.Dictionary => "dictionary",
            PdfObjectType.Stream => "stream",
            PdfObjectType.IndirectReference => "indirect reference",
            _ => "Unknown",
        };
    }
}
