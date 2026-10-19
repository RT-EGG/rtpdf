using System.Text;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Documents;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Pages;
using Rtpdf.Core.Parsing;
using Rtpdf.Core.Storage;
using Xunit;

namespace Rtpdf.Core.Tests.Documents;

public sealed class PdfCatalogReaderTests
{
    [Fact]
    public void ReadBuildsHierarchyAndResolvesInheritedPageAttributes()
    {
        var document = CreateValidDocument();

        var catalog = CreateReader(document).Read();

        Assert.Equal(new PdfObjectId(1), catalog.ObjectId);
        Assert.Equal(new PdfObjectId(2), catalog.PageTree.Root.ObjectId);
        Assert.Equal(2, catalog.PageTree.Root.DeclaredPageCount);
        Assert.Equal(2, catalog.PageTree.Count);
        Assert.IsType<PdfPage>(catalog.PageTree.Root.Kids[0]);
        var nestedNode = Assert.IsType<PdfPageTreeNode>(catalog.PageTree.Root.Kids[1]);
        Assert.Single(nestedNode.Kids);

        var first = catalog.PageTree.Pages[0];
        Assert.Equal(0, first.Index);
        Assert.Equal(new PdfObjectId(3), first.ObjectId);
        Assert.Equal(new PdfRectangle(0, 0, 612, 792), first.MediaBox);
        Assert.Equal(new PdfRectangle(10, 20, 600, 780), first.CropBox);
        Assert.NotNull(first.Resources);
        Assert.Equal(90, first.Rotation);

        var second = catalog.PageTree.Pages[1];
        Assert.Equal(1, second.Index);
        Assert.Equal(new PdfObjectId(5), second.ObjectId);
        Assert.Equal(second.MediaBox, second.CropBox);
        Assert.Same(first.Resources, second.Resources);
        Assert.Equal(270, second.Rotation);
        Assert.Equal(7, document.Store.CachedObjectCount);
    }

    [Fact]
    public void ReadRequiresAnIndirectTrailerRootAndCatalogPagesReference()
    {
        var missingRoot = CreateDocument(string.Empty,
            new ObjectDefinition(1, "<< /Type /Catalog /Pages 2 0 R >>"),
            new ObjectDefinition(2, "<< /Type /Pages /Kids [] /Count 0 >>"));
        var directPages = CreateDocument("/Root 1 0 R",
            new ObjectDefinition(1, "<< /Type /Catalog /Pages << >> >>"));

        Assert.Throws<PdfDocumentStructureException>(() => CreateReader(missingRoot).Read());
        Assert.Throws<PdfDocumentStructureException>(() => CreateReader(directPages).Read());
    }

    [Fact]
    public void ReadValidatesCatalogAndPageTreeTypes()
    {
        var wrongCatalog = CreateDocument("/Root 1 0 R",
            new ObjectDefinition(1, "<< /Type /NotCatalog /Pages 2 0 R >>"),
            new ObjectDefinition(2, "<< /Type /Pages /Kids [] /Count 0 >>"));
        var wrongPageTreeType = CreateDocument("/Root 1 0 R",
            new ObjectDefinition(1, "<< /Type /Catalog /Pages 2 0 R >>"),
            new ObjectDefinition(2, "<< /Type /Other >>"));

        Assert.Throws<PdfDocumentStructureException>(() => CreateReader(wrongCatalog).Read());
        Assert.Throws<PdfDocumentStructureException>(() => CreateReader(wrongPageTreeType).Read());
    }

    [Fact]
    public void ReadRejectsCyclesAndDuplicatePlacements()
    {
        var cyclic = CreateDocument("/Root 1 0 R",
            new ObjectDefinition(1, "<< /Type /Catalog /Pages 2 0 R >>"),
            new ObjectDefinition(2, "<< /Type /Pages /Kids [3 0 R] /Count 0 >>"),
            new ObjectDefinition(3, "<< /Type /Pages /Parent 2 0 R /Kids [2 0 R] /Count 0 >>"));
        var duplicate = CreateDocument("/Root 1 0 R",
            new ObjectDefinition(1, "<< /Type /Catalog /Pages 2 0 R >>"),
            new ObjectDefinition(2, "<< /Type /Pages /Kids [3 0 R 3 0 R] /Count 2 >>"),
            new ObjectDefinition(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 1 1] >>"));

        var cycleError = Assert.Throws<PdfDocumentStructureException>(() => CreateReader(cyclic).Read());
        var duplicateError = Assert.Throws<PdfDocumentStructureException>(() => CreateReader(duplicate).Read());

        Assert.Contains("Cyclic", cycleError.Message);
        Assert.Contains("more than once", duplicateError.Message);
    }

    [Fact]
    public void ReadValidatesParentReferencesAndDeclaredCounts()
    {
        var missingParent = CreateDocument("/Root 1 0 R",
            new ObjectDefinition(1, "<< /Type /Catalog /Pages 2 0 R >>"),
            new ObjectDefinition(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            new ObjectDefinition(3, "<< /Type /Page /MediaBox [0 0 1 1] >>"));
        var wrongCount = CreateDocument("/Root 1 0 R",
            new ObjectDefinition(1, "<< /Type /Catalog /Pages 2 0 R >>"),
            new ObjectDefinition(2, "<< /Type /Pages /Kids [3 0 R] /Count 2 >>"),
            new ObjectDefinition(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 1 1] >>"));

        Assert.Throws<PdfDocumentStructureException>(() => CreateReader(missingParent).Read());
        Assert.Throws<PdfDocumentStructureException>(() => CreateReader(wrongCount).Read());
    }

    [Fact]
    public void ValidationForParentsAndCountsCanBeDisabled()
    {
        var document = CreateDocument("/Root 1 0 R",
            new ObjectDefinition(1, "<< /Type /Catalog /Pages 2 0 R >>"),
            new ObjectDefinition(2, "<< /Type /Pages /Kids [3 0 R] /Count 99 >>"),
            new ObjectDefinition(3, "<< /Type /Page /MediaBox [0 0 1 1] >>"));
        var options = new PdfCatalogReaderOptions
        {
            ValidatePageCounts = false,
            ValidateParentReferences = false,
        };

        var catalog = CreateReader(document, options).Read();

        Assert.Single(catalog.PageTree.Pages);
        Assert.Equal(99, catalog.PageTree.Root.DeclaredPageCount);
    }

    [Fact]
    public void ReadRequiresValidMediaAndCropBoxes()
    {
        var missingMediaBox = CreateSinglePageDocument(string.Empty);
        var malformedCropBox = CreateSinglePageDocument(
            "/MediaBox [0 0 10 10] /CropBox [0 0 10]");
        var invertedMediaBox = CreateSinglePageDocument("/MediaBox [10 0 0 10]");

        Assert.Throws<PdfDocumentStructureException>(() => CreateReader(missingMediaBox).Read());
        Assert.Throws<PdfDocumentStructureException>(() => CreateReader(malformedCropBox).Read());
        Assert.Throws<PdfDocumentStructureException>(() => CreateReader(invertedMediaBox).Read());
    }

    [Fact]
    public void ReadResolvesIndirectRectangleElementsAndDefaultsRotation()
    {
        var document = CreateDocument("/Root 1 0 R",
            new ObjectDefinition(1, "<< /Type /Catalog /Pages 2 0 R >>"),
            new ObjectDefinition(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            new ObjectDefinition(3, "<< /Type /Page /Parent 2 0 R /MediaBox 4 0 R >>"),
            new ObjectDefinition(4, "[0 0 5 0 R 6 0 R]"),
            new ObjectDefinition(5, "100.5"),
            new ObjectDefinition(6, "200"));

        var page = Assert.Single(CreateReader(document).Read().PageTree.Pages);

        Assert.Equal(new PdfRectangle(0, 0, 100.5m, 200), page.MediaBox);
        Assert.Equal(0, page.Rotation);
    }

    [Fact]
    public void ReadRejectsInvalidRotationResourcesAndKids()
    {
        var invalidRotation = CreateSinglePageDocument("/MediaBox [0 0 1 1] /Rotate 45");
        var invalidResources = CreateSinglePageDocument("/MediaBox [0 0 1 1] /Resources []");
        var directKid = CreateDocument("/Root 1 0 R",
            new ObjectDefinition(1, "<< /Type /Catalog /Pages 2 0 R >>"),
            new ObjectDefinition(2, "<< /Type /Pages /Kids [<< /Type /Page >>] /Count 1 >>"));

        Assert.Throws<PdfDocumentStructureException>(() => CreateReader(invalidRotation).Read());
        Assert.Throws<PdfDocumentStructureException>(() => CreateReader(invalidResources).Read());
        Assert.Throws<PdfDocumentStructureException>(() => CreateReader(directKid).Read());
    }

    [Fact]
    public void ReadEnforcesTraversalLimits()
    {
        AssertLimitFails(new PdfCatalogReaderOptions { MaximumPageTreeDepth = 2 });
        AssertLimitFails(new PdfCatalogReaderOptions { MaximumPageTreeNodes = 2 });
        AssertLimitFails(new PdfCatalogReaderOptions { MaximumPages = 1 });
        AssertLimitFails(new PdfCatalogReaderOptions { MaximumKidsPerNode = 1 });
    }

    [Fact]
    public void ConstructorRejectsInvalidLimits()
    {
        var document = CreateValidDocument();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateReader(document, new PdfCatalogReaderOptions { MaximumPages = 0 }));
    }

    [Fact]
    public void ResolutionFailuresAreReportedAsDocumentStructureErrors()
    {
        var document = CreateDocument("/Root 9 0 R",
            new ObjectDefinition(1, "null"));

        var exception = Assert.Throws<PdfDocumentStructureException>(() => CreateReader(document).Read());

        Assert.Equal(new PdfObjectId(9), exception.ObjectId);
        Assert.IsType<PdfObjectResolutionException>(exception.InnerException);
    }

    private static void AssertLimitFails(PdfCatalogReaderOptions options) =>
        Assert.Throws<PdfDocumentStructureException>(() => CreateReader(CreateValidDocument(), options).Read());

    private static PdfCatalogReader CreateReader(
        TestDocument document,
        PdfCatalogReaderOptions? options = null) =>
        new(document.Table, document.Store, options);

    private static TestDocument CreateValidDocument() =>
        CreateDocument("/Root 1 0 R",
            new ObjectDefinition(1, "<< /Type /Catalog /Pages 2 0 R >>"),
            new ObjectDefinition(2, "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 /MediaBox 6 0 R /Resources 7 0 R /Rotate 90 >>"),
            new ObjectDefinition(3, "<< /Type /Page /Parent 2 0 R /CropBox [10 20 600 780] >>"),
            new ObjectDefinition(4, "<< /Type /Pages /Parent 2 0 R /Kids [5 0 R] /Count 1 /Rotate -90 >>"),
            new ObjectDefinition(5, "<< /Type /Page /Parent 4 0 R >>"),
            new ObjectDefinition(6, "[0 0 612 792]"),
            new ObjectDefinition(7, "<< /Font << >> >>"));

    private static TestDocument CreateSinglePageDocument(string pageEntries) =>
        CreateDocument("/Root 1 0 R",
            new ObjectDefinition(1, "<< /Type /Catalog /Pages 2 0 R >>"),
            new ObjectDefinition(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            new ObjectDefinition(3, $"<< /Type /Page /Parent 2 0 R {pageEntries} >>"));

    private static TestDocument CreateDocument(
        string trailerEntries,
        params ObjectDefinition[] definitions)
    {
        var ordered = definitions.OrderBy(definition => definition.ObjectNumber).ToArray();
        var maximumObjectNumber = ordered.Length == 0 ? 0 : ordered[^1].ObjectNumber;
        var source = new StringBuilder("%PDF-1.7\n");
        var offsets = new Dictionary<int, int>();
        var byNumber = ordered.ToDictionary(definition => definition.ObjectNumber);

        foreach (var definition in ordered)
        {
            offsets.Add(definition.ObjectNumber, Encoding.ASCII.GetByteCount(source.ToString()));
            source.Append(definition.ObjectNumber)
                .Append(" 0 obj\n")
                .Append(definition.Value)
                .Append("\nendobj\n");
        }

        var crossReferenceOffset = Encoding.ASCII.GetByteCount(source.ToString());
        source.Append("xref\n0 ")
            .Append(maximumObjectNumber + 1)
            .Append("\n0000000000 65535 f \n");

        for (var objectNumber = 1; objectNumber <= maximumObjectNumber; objectNumber++)
        {
            if (byNumber.ContainsKey(objectNumber))
            {
                source.Append(offsets[objectNumber].ToString("D10"))
                    .Append(" 00000 n \n");
            }
            else
            {
                source.Append("0000000000 00000 f \n");
            }
        }

        source.Append("trailer\n<< /Size ")
            .Append(maximumObjectNumber + 1)
            .Append(' ')
            .Append(trailerEntries)
            .Append(" >>\nstartxref\n")
            .Append(crossReferenceOffset)
            .Append("\n%%EOF\n");

        var bytes = Encoding.ASCII.GetBytes(source.ToString());
        var table = new PdfCrossReferenceParser(bytes).Parse();
        return new TestDocument(bytes, table, new PdfObjectStore(bytes, table));
    }

    private sealed record ObjectDefinition(int ObjectNumber, string Value);

    private sealed record TestDocument(
        byte[] Bytes,
        Rtpdf.Core.CrossReferences.PdfCrossReferenceTable Table,
        PdfObjectStore Store);
}
