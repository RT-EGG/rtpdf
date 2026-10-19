using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Pages;

/// <summary>
/// Base class for page-tree branch and leaf objects.
/// </summary>
public abstract class PdfPageTreeItem
{
    private protected PdfPageTreeItem(PdfObjectId objectId, PdfDictionary dictionary)
    {
        ObjectId = objectId;
        Dictionary = dictionary;
    }

    public PdfObjectId ObjectId { get; }

    public PdfDictionary Dictionary { get; }
}
