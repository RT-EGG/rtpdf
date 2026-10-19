using System.Text;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Parsing;
using Rtpdf.Core.Storage;
using Xunit;

namespace Rtpdf.Core.Tests.Storage;

public sealed class PdfObjectStoreTests
{
    [Fact]
    public void ResolveLoadsCatalogAndLeavesNestedReferencesExplicit()
    {
        var document = CreateDocument(
            new ObjectDefinition(1, 0, "<< /Type /Catalog /Pages 2 0 R >>"),
            new ObjectDefinition(2, 0, "[3 0 R]"),
            new ObjectDefinition(3, 0, "(leaf)"));
        var store = new PdfObjectStore(document.Bytes, document.Table);

        var catalog = store.Resolve<PdfDictionary>(new PdfIndirectReference(1));
        var pagesReference = Assert.IsType<PdfIndirectReference>(catalog["Pages"]);
        var pages = store.Resolve<PdfArray>(pagesReference);

        Assert.Equal(new PdfIndirectReference(3), pages[0]);
        Assert.Equal(2, store.CachedObjectCount);
    }

    [Fact]
    public void LoadedObjectsAreCachedByObjectIdentity()
    {
        var document = CreateDocument(new ObjectDefinition(1, 0, "<< /Value 42 >>"));
        var store = new PdfObjectStore(document.Bytes, document.Table);

        var first = store.GetIndirectObject(new PdfObjectId(1));
        var second = store.GetIndirectObject(new PdfObjectId(1));

        Assert.Same(first, second);
        Assert.True(store.TryGetCachedObject(new PdfObjectId(1), out var cached));
        Assert.Same(first, cached);
        Assert.Equal(1, store.CachedObjectCount);
    }

    [Fact]
    public void ResolveFollowsAReferenceStoredAsAnIndirectObjectsValue()
    {
        var document = CreateDocument(
            new ObjectDefinition(1, 0, "2 0 R"),
            new ObjectDefinition(2, 0, "(target)"));
        var store = new PdfObjectStore(document.Bytes, document.Table);

        var result = store.Resolve<PdfString>(new PdfIndirectReference(1));

        Assert.Equal("target", result.GetText());
        Assert.Equal(2, store.CachedObjectCount);
    }

    [Fact]
    public void ResolveRejectsMissingFreeAndMismatchedGenerationEntries()
    {
        var document = CreateDocument(
            new ObjectDefinition(1, 2, "(one)"),
            new ObjectDefinition(3, 0, "(three)"));
        var store = new PdfObjectStore(document.Bytes, document.Table);

        Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve(new PdfIndirectReference(4)));
        Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve(new PdfIndirectReference(2)));
        Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve(new PdfIndirectReference(1, 0)));
    }

    [Fact]
    public void ResolveRejectsObjectHeaderThatDisagreesWithXrefEntry()
    {
        var document = CreateDocument(new ObjectDefinition(1, 0, "(value)"));
        document.Bytes[document.ObjectOffsets[1]] = (byte)'2';
        var store = new PdfObjectStore(document.Bytes, document.Table);

        var exception = Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve(new PdfIndirectReference(1)));

        Assert.Contains("identifies", exception.Message);
    }

    [Fact]
    public void ResolveWrapsMalformedIndirectObjectSyntax()
    {
        var document = CreateDocument(new ObjectDefinition(1, 0, "(value)"));
        var endobj = FindBytes(document.Bytes, "endobj"u8);
        document.Bytes[endobj + "endobj"u8.Length - 1] = (byte)'x';
        var store = new PdfObjectStore(document.Bytes, document.Table);

        var exception = Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve(new PdfIndirectReference(1)));

        Assert.IsType<PdfParseException>(exception.InnerException);
        Assert.Equal(document.ObjectOffsets[1], exception.Offset);
    }

    [Fact]
    public void ResolveLoadsStreamWhoseLengthIsAnIndirectObject()
    {
        var document = CreateDocument(
            new ObjectDefinition(1, 0, "<< /Length 2 0 R >>\nstream\nhello\nendstream"),
            new ObjectDefinition(2, 0, "5"));
        var store = new PdfObjectStore(document.Bytes, document.Table);

        var stream = store.Resolve<PdfStream>(new PdfIndirectReference(1));

        Assert.Equal("hello"u8.ToArray(), stream.GetEncodedDataCopy());
        Assert.Equal(2, store.CachedObjectCount);
    }

    [Fact]
    public void ResolveRejectsCyclicStreamLengthDependency()
    {
        var document = CreateDocument(
            new ObjectDefinition(1, 0, "<< /Length 1 0 R >>\nstream\n\nendstream"));
        var store = new PdfObjectStore(document.Bytes, document.Table);

        var exception = Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve(new PdfIndirectReference(1)));

        Assert.Contains("dependency", exception.Message);
        Assert.IsType<PdfObjectResolutionException>(exception.InnerException);
    }

    [Fact]
    public void ResolveDetectsCyclicReferenceChains()
    {
        var document = CreateDocument(
            new ObjectDefinition(1, 0, "2 0 R"),
            new ObjectDefinition(2, 0, "1 0 R"));
        var store = new PdfObjectStore(document.Bytes, document.Table);

        var exception = Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve(new PdfIndirectReference(1)));

        Assert.Contains("Cyclic", exception.Message);
    }

    [Fact]
    public void ResolveEnforcesReferenceDepthLimit()
    {
        var document = CreateDocument(
            new ObjectDefinition(1, 0, "2 0 R"),
            new ObjectDefinition(2, 0, "3 0 R"),
            new ObjectDefinition(3, 0, "(target)"));
        var options = new PdfObjectStoreOptions { MaximumReferenceDepth = 2 };
        var store = new PdfObjectStore(document.Bytes, document.Table, options);

        Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve(new PdfIndirectReference(1)));
    }

    [Fact]
    public void ObjectCacheLimitIsEnforced()
    {
        var document = CreateDocument(
            new ObjectDefinition(1, 0, "(one)"),
            new ObjectDefinition(2, 0, "(two)"));
        var options = new PdfObjectStoreOptions { MaximumCachedObjects = 1 };
        var store = new PdfObjectStore(document.Bytes, document.Table, options);

        store.Resolve(new PdfIndirectReference(1));

        Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve(new PdfIndirectReference(2)));
        Assert.Equal(1, store.CachedObjectCount);
    }

    [Fact]
    public void TypedResolveRejectsUnexpectedObjectType()
    {
        var document = CreateDocument(new ObjectDefinition(1, 0, "(text)"));
        var store = new PdfObjectStore(document.Bytes, document.Table);

        Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve<PdfDictionary>(new PdfIndirectReference(1)));
    }

    [Fact]
    public void StreamInputUsesOffsetsRelativeToItsCurrentPositionAndRemainsOpen()
    {
        var document = CreateDocument(new ObjectDefinition(1, 0, "(text)"));
        using var stream = new MemoryStream([1, 2, 3, .. document.Bytes]);
        stream.Position = 3;
        var store = new PdfObjectStore(stream, document.Table);

        var result = store.Resolve<PdfString>(new PdfIndirectReference(1));

        Assert.Equal("text", result.GetText());
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void InvalidStoreLimitsAreRejected(int maximumDepth, int maximumCachedObjects)
    {
        var document = CreateDocument(new ObjectDefinition(1, 0, "null"));
        var options = new PdfObjectStoreOptions
        {
            MaximumReferenceDepth = maximumDepth,
            MaximumCachedObjects = maximumCachedObjects,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfObjectStore(document.Bytes, document.Table, options));
    }

    private static TestDocument CreateDocument(params ObjectDefinition[] definitions)
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
                .Append(' ')
                .Append(definition.GenerationNumber)
                .Append(" obj\n")
                .Append(definition.Value)
                .Append("\nendobj\n");
        }

        var crossReferenceOffset = Encoding.ASCII.GetByteCount(source.ToString());
        source.Append("xref\n0 ")
            .Append(maximumObjectNumber + 1)
            .Append("\n0000000000 65535 f \n");

        for (var objectNumber = 1; objectNumber <= maximumObjectNumber; objectNumber++)
        {
            if (byNumber.TryGetValue(objectNumber, out var definition))
            {
                source.Append(offsets[objectNumber].ToString("D10"))
                    .Append(' ')
                    .Append(definition.GenerationNumber.ToString("D5"))
                    .Append(" n \n");
            }
            else
            {
                source.Append("0000000000 00000 f \n");
            }
        }

        source.Append("trailer\n<< /Size ")
            .Append(maximumObjectNumber + 1)
            .Append(" >>\nstartxref\n")
            .Append(crossReferenceOffset)
            .Append("\n%%EOF\n");

        var bytes = Encoding.ASCII.GetBytes(source.ToString());
        var table = new PdfCrossReferenceParser(bytes).Parse();
        return new TestDocument(bytes, table, offsets);
    }

    private static int FindBytes(byte[] input, ReadOnlySpan<byte> value)
    {
        var index = input.AsSpan().IndexOf(value);
        Assert.True(index >= 0);
        return index;
    }

    private sealed record ObjectDefinition(int ObjectNumber, ushort GenerationNumber, string Value);

    private sealed record TestDocument(
        byte[] Bytes,
        Rtpdf.Core.CrossReferences.PdfCrossReferenceTable Table,
        IReadOnlyDictionary<int, int> ObjectOffsets);
}
