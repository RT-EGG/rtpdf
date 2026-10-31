using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Pages;

namespace Rtpdf.Core.Writing.Pages;

/// <summary>
/// Builds a flat page tree and its document catalog for a newly generated PDF.
/// </summary>
public sealed class PdfPageTreeBuilder
{
    private readonly PdfDocumentBuilder _document;
    private readonly PdfArray _kids = [];
    private readonly List<PdfPageBuilder> _pages = [];

    public PdfPageTreeBuilder(PdfDocumentBuilder document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        if (document.Root is not null)
        {
            throw new PdfWriteException(
                "The document already has a trailer /Root; a page catalog cannot be attached twice");
        }

        RootDictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("Pages"),
            ["Kids"] = _kids,
            ["Count"] = new PdfInteger(0),
        };
        RootObject = document.AddObject(RootDictionary);

        CatalogDictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("Catalog"),
            ["Pages"] = RootObject.CreateReference(),
        };
        CatalogObject = document.AddObject(CatalogDictionary);
        document.Root = CatalogObject.CreateReference();
        Pages = _pages.AsReadOnly();
    }

    public PdfIndirectObject RootObject { get; }

    public PdfDictionary RootDictionary { get; }

    public PdfIndirectObject CatalogObject { get; }

    public PdfDictionary CatalogDictionary { get; }

    public IReadOnlyList<PdfPageBuilder> Pages { get; }

    public int Count => _pages.Count;

    /// <summary>Adds a page leaf to the end of this page tree.</summary>
    public PdfPageBuilder AddPage(PdfRectangle mediaBox)
    {
        var page = new PdfPageBuilder(
            _document,
            RootObject.CreateReference(),
            mediaBox,
            _pages.Count);
        _pages.Add(page);
        _kids.Add(page.Reference);
        RootDictionary["Count"] = new PdfInteger(_pages.Count);
        return page;
    }
}
