using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Documents;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Pages;
using Rtpdf.Core.Writing;
using Rtpdf.Core.Writing.Pages;
using Xunit;

namespace Rtpdf.Core.Tests.Writing;

public sealed class PdfPageBuilderTests
{
    [Fact]
    public void BuildsAFlatPageTreeThatCanBeReadBack()
    {
        var document = new PdfDocumentBuilder();
        var pages = new PdfPageTreeBuilder(document);
        var first = pages.AddPage(new PdfRectangle(0, 0, 595, 842));
        var second = pages.AddPage(new PdfRectangle(10.5m, 20.25m, 410.5m, 620.25m));
        second.CropBox = new PdfRectangle(20, 30, 400, 600);
        second.Rotation = -90;

        using var destination = new MemoryStream();
        PdfDocumentWriter.Write(destination, document, leaveOpen: true);
        using var parsed = PdfDocumentReader.Open(destination.ToArray().AsMemory());

        Assert.Equal(2, pages.Count);
        Assert.Equal(0, first.Index);
        Assert.Equal(1, second.Index);
        Assert.Equal(2, parsed.Catalog.PageTree.Count);
        Assert.Equal(new PdfRectangle(0, 0, 595, 842), parsed.Catalog.PageTree.Pages[0].MediaBox);
        Assert.Equal(new PdfRectangle(10.5m, 20.25m, 410.5m, 620.25m), parsed.Catalog.PageTree.Pages[1].MediaBox);
        Assert.Equal(new PdfRectangle(20, 30, 400, 600), parsed.Catalog.PageTree.Pages[1].CropBox);
        Assert.Equal(270, parsed.Catalog.PageTree.Pages[1].Rotation);

        var kids = Assert.IsType<PdfArray>(pages.RootDictionary["Kids"]);
        Assert.Equal(2, kids.Count);
        Assert.Equal(new PdfInteger(2), pages.RootDictionary["Count"]);
        Assert.Equal(pages.CatalogObject.CreateReference(), document.Root);
    }

    [Fact]
    public void AddsContentStreamsAndNamedResources()
    {
        var document = new PdfDocumentBuilder();
        var page = new PdfPageTreeBuilder(document)
            .AddPage(new PdfRectangle(0, 0, 100, 200));
        var image = document.AddObject(new PdfStream(
            new PdfDictionary
            {
                ["Type"] = new PdfName("XObject"),
                ["Subtype"] = new PdfName("Image"),
            },
            [1, 2, 3]));

        page.SetResource("XObject", "Image1", image.CreateReference());
        var firstContent = page.AddContent(new PdfStream(new PdfDictionary(), "q Q"u8));
        var secondContent = page.AddContent(new PdfStream(new PdfDictionary(), "q Q"u8));

        var xObjects = Assert.IsType<PdfDictionary>(page.Resources["XObject"]);
        Assert.Equal(image.CreateReference(), xObjects["Image1"]);
        Assert.Equal(firstContent, page.Contents[0]);
        Assert.Equal(secondContent, page.Contents[1]);
        Assert.Equal(2, Assert.IsType<PdfArray>(page.Dictionary["Contents"]).Count);

        using var destination = new MemoryStream();
        PdfDocumentWriter.Write(destination, document, leaveOpen: true);
        using var parsed = PdfDocumentReader.Open(destination.ToArray().AsMemory());
        var parsedPage = Assert.Single(parsed.Catalog.PageTree.Pages);
        var parsedXObjects = Assert.IsType<PdfDictionary>(parsedPage.Resources!["XObject"]);
        Assert.Equal(image.CreateReference(), parsedXObjects["Image1"]);
    }

    [Fact]
    public void UpdatesPageDictionaryWhenPropertiesChange()
    {
        var page = new PdfPageTreeBuilder(new PdfDocumentBuilder())
            .AddPage(new PdfRectangle(0, 0, 100, 100));

        page.MediaBox = new PdfRectangle(-10, -20, 200, 300);
        page.CropBox = new PdfRectangle(0, 0, 100, 200);
        page.Rotation = 450;

        Assert.Equal(
            new PdfObject[]
            {
                new PdfInteger(-10),
                new PdfInteger(-20),
                new PdfInteger(200),
                new PdfInteger(300),
            },
            Assert.IsType<PdfArray>(page.Dictionary["MediaBox"]));
        Assert.Equal(new PdfInteger(90), page.Dictionary["Rotate"]);

        page.CropBox = null;
        page.Rotation = 0;
        Assert.False(page.Dictionary.ContainsKey("CropBox"));
        Assert.False(page.Dictionary.ContainsKey("Rotate"));
    }

    [Fact]
    public void RejectsInvalidPageGeometryAndReferences()
    {
        var document = new PdfDocumentBuilder();
        var pages = new PdfPageTreeBuilder(document);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            pages.AddPage(new PdfRectangle(0, 0, 0, 100)));

        var page = pages.AddPage(new PdfRectangle(0, 0, 100, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.Rotation = 45);
        Assert.Throws<PdfWriteException>(() =>
            page.AddContent(new PdfIndirectReference(999)));
        Assert.Throws<PdfWriteException>(() =>
            page.SetResource("XObject", "Image1", new PdfIndirectReference(999)));
        Assert.Throws<PdfWriteException>(() =>
            page.SetResource("XObject", "Image1", new PdfStream(new PdfDictionary(), [])));

        var notAStream = document.AddObject(new PdfDictionary()).CreateReference();
        Assert.Throws<PdfWriteException>(() => page.AddContent(notAStream));
    }

    [Fact]
    public void RejectsAttachingASecondCatalogToTheSameDocument()
    {
        var document = new PdfDocumentBuilder();
        _ = new PdfPageTreeBuilder(document);

        Assert.Throws<PdfWriteException>(() => new PdfPageTreeBuilder(document));
    }
}
