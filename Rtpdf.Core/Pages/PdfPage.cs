using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Pages;

/// <summary>
/// A page leaf with its resolved inheritable page attributes.
/// </summary>
public sealed class PdfPage : PdfPageTreeItem
{
    internal PdfPage(
        PdfObjectId objectId,
        PdfDictionary dictionary,
        int index,
        PdfRectangle mediaBox,
        PdfRectangle cropBox,
        PdfDictionary? resources,
        int rotation)
        : base(objectId, dictionary)
    {
        Index = index;
        MediaBox = mediaBox;
        CropBox = cropBox;
        Resources = resources;
        Rotation = rotation;
    }

    public int Index { get; }

    public PdfRectangle MediaBox { get; }

    public PdfRectangle CropBox { get; }

    public PdfDictionary? Resources { get; }

    /// <summary>Gets clockwise rotation normalized to 0, 90, 180, or 270 degrees.</summary>
    public int Rotation { get; }
}
