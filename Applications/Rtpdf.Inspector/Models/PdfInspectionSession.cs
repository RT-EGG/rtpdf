using Rtpdf.Core.CrossReferences;
using Rtpdf.Core.Documents;
using Rtpdf.Core.Filters;
using Rtpdf.Core.Storage;

namespace Rtpdf.Inspector.Models;

internal sealed record PdfInspectionSession(
    byte[] Source,
    PdfCrossReferenceTable CrossReferences,
    PdfObjectStore ObjectStore,
    PdfCatalog Catalog,
    PdfStreamDecoder StreamDecoder
);