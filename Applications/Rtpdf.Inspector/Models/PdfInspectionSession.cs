using Rtpdf.Core.CrossReferences;
using Rtpdf.Core.Documents;
using Rtpdf.Core.Filters;
using Rtpdf.Core.Storage;
using System;

namespace Rtpdf.Inspector.Models;

internal sealed record PdfInspectionSession(PdfDocument Document) : IDisposable
{
    public PdfCrossReferenceTable CrossReferences => Document.CrossReferences;

    public PdfObjectStore ObjectStore => Document.ObjectStore;

    public PdfCatalog Catalog => Document.Catalog;

    public PdfStreamDecoder StreamDecoder => Document.StreamDecoder;

    public void Dispose() => Document.Dispose();
}
