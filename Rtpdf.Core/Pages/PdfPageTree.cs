namespace Rtpdf.Core.Pages;

/// <summary>
/// A validated page-tree hierarchy and its page leaves in document order.
/// </summary>
public sealed class PdfPageTree
{
    internal PdfPageTree(PdfPageTreeNode root, IReadOnlyList<PdfPage> pages)
    {
        Root = root;
        Pages = pages;
    }

    public PdfPageTreeNode Root { get; }

    public IReadOnlyList<PdfPage> Pages { get; }

    public int Count => Pages.Count;
}
