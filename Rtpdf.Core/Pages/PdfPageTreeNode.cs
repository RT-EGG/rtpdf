using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Pages;

/// <summary>
/// A <c>/Pages</c> branch in a PDF page tree.
/// </summary>
public sealed class PdfPageTreeNode : PdfPageTreeItem
{
    internal PdfPageTreeNode(
        PdfObjectId objectId,
        PdfDictionary dictionary,
        int declaredPageCount,
        IReadOnlyList<PdfPageTreeItem> kids)
        : base(objectId, dictionary)
    {
        DeclaredPageCount = declaredPageCount;
        Kids = kids;
    }

    public int DeclaredPageCount { get; }

    public IReadOnlyList<PdfPageTreeItem> Kids { get; }
}
