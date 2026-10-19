using Rtpdf.Core.Objects;
using Rtpdf.Core.Pages;

namespace Rtpdf.Core.Documents;

/// <summary>
/// A validated PDF document catalog and its page tree.
/// </summary>
public sealed class PdfCatalog
{
    internal PdfCatalog(PdfObjectId objectId, PdfDictionary dictionary, PdfPageTree pageTree)
    {
        ObjectId = objectId;
        Dictionary = dictionary;
        PageTree = pageTree;
    }

    public PdfObjectId ObjectId { get; }

    public PdfDictionary Dictionary { get; }

    public PdfPageTree PageTree { get; }
}
