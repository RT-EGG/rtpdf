using System.IO.Compression;
using System.Text;
using Rtpdf.Core.CrossReferences;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Documents;
using Rtpdf.Core.Filters;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Parsing;
using Rtpdf.Core.Storage;
using Xunit;

namespace Rtpdf.Core.Tests.Storage;

public sealed class PdfObjectStreamTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolveReadsPlainAndFlateEncodedObjectStreams(bool useFlate)
    {
        var content = CreateValidContent();
        var document = CreateDocument(
            content.DecodedData,
            $"/Type /ObjStm /N 3 /First {content.FirstObjectOffset}",
            [new(1, 0), new(2, 1), new(3, 2)],
            useFlate);
        var table = new PdfCrossReferenceParser(document).Parse();
        var store = new PdfObjectStore(document, table);

        var catalog = new PdfCatalogReader(table, store).Read();
        var text = store.Resolve<PdfString>(new PdfIndirectReference(3));

        Assert.Equal(new PdfObjectId(1), catalog.ObjectId);
        Assert.Empty(catalog.PageTree.Pages);
        Assert.Equal("hello", text.GetText());
        Assert.Equal(1, store.CachedObjectStreamCount);
        Assert.Equal(4, store.CachedObjectCount);
        Assert.Same(
            store.GetIndirectObject(new PdfObjectId(3)),
            store.GetIndirectObject(new PdfObjectId(3)));
    }

    [Fact]
    public void ResolveRejectsAnXrefIndexThatIdentifiesAnotherObject()
    {
        var content = CreateValidContent();
        var document = CreateDocument(
            content.DecodedData,
            $"/Type /ObjStm /N 3 /First {content.FirstObjectOffset}",
            [new(1, 1)]);
        var store = CreateStore(document);

        var exception = Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve(new PdfIndirectReference(1)));

        Assert.Contains("Object stream data is invalid", exception.Message);
        Assert.Contains("identifies object 2", exception.InnerException?.Message);
    }

    [Fact]
    public void ResolveRejectsAnIndirectReferenceAsTheEntireCompressedObject()
    {
        var content = CreateContent((1, "2 0 R"));
        var document = CreateDocument(
            content.DecodedData,
            $"/Type /ObjStm /N 1 /First {content.FirstObjectOffset}",
            [new(1, 0)]);
        var store = CreateStore(document);

        var exception = Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve(new PdfIndirectReference(1)));

        Assert.Contains("must not consist solely", exception.InnerException?.Message);
    }

    [Fact]
    public void ResolveRejectsAStreamObjectInsideAnObjectStream()
    {
        var content = CreateContent((1, "<< /Length 0 >> stream\n\nendstream"));
        var document = CreateDocument(
            content.DecodedData,
            $"/Type /ObjStm /N 1 /First {content.FirstObjectOffset}",
            [new(1, 0)]);
        var store = CreateStore(document);

        var exception = Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve(new PdfIndirectReference(1)));

        Assert.Contains("Object stream data is invalid", exception.Message);
    }

    [Theory]
    [MemberData(nameof(MalformedObjectStreams))]
    public void ResolveRejectsMalformedObjectStreams(
        byte[] decodedData,
        string dictionaryEntries,
        int objectIndex)
    {
        var document = CreateDocument(
            decodedData,
            dictionaryEntries,
            [new(1, objectIndex)]);
        var store = CreateStore(document);

        Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve(new PdfIndirectReference(1)));
    }

    [Fact]
    public void ResolveRequiresTheObjectStreamItselfToBeUncompressed()
    {
        var content = CreateContent((1, "null"));
        var document = CreateDocument(
            content.DecodedData,
            $"/Type /ObjStm /N 1 /First {content.FirstObjectOffset}",
            [new(1, 0)],
            objectStreamIsCompressed: true);
        var store = CreateStore(document);

        var exception = Assert.Throws<PdfObjectResolutionException>(() =>
            store.Resolve(new PdfIndirectReference(1)));

        Assert.Contains("dependency", exception.Message);
        Assert.Contains("uncompressed generation-0", exception.InnerException?.Message);
    }

    [Fact]
    public void ResolveRejectsAnEncryptionDictionaryInAnObjectStream()
    {
        var content = CreateContent((1, "<< /Filter /Standard >>"));
        var document = CreateDocument(
            content.DecodedData,
            $"/Type /ObjStm /N 1 /First {content.FirstObjectOffset}",
            [new(1, 0)],
            xrefTrailerEntries: "/Encrypt 1 0 R");
        var store = CreateStore(document);

        var exception = Assert.Throws<PdfObjectResolutionException>(() =>
            store.GetIndirectObject(new PdfObjectId(1)));

        Assert.Contains("encryption dictionary", exception.Message);
    }

    [Fact]
    public void ResolveAppliesObjectStreamResourceLimits()
    {
        var content = CreateContent((1, "(hello)"));
        var document = CreateDocument(
            content.DecodedData,
            $"/Type /ObjStm /N 1 /First {content.FirstObjectOffset}",
            [new(1, 0)]);
        var table = new PdfCrossReferenceParser(document).Parse();

        Assert.Throws<PdfObjectResolutionException>(() =>
            new PdfObjectStore(document, table, new PdfObjectStoreOptions
            {
                MaximumObjectsPerObjectStream = 0,
            }).Resolve(new PdfIndirectReference(1)));
        Assert.Throws<PdfObjectResolutionException>(() =>
            new PdfObjectStore(document, table, new PdfObjectStoreOptions
            {
                MaximumCachedObjectStreams = 0,
            }).Resolve(new PdfIndirectReference(1)));
        Assert.Throws<PdfObjectResolutionException>(() =>
            new PdfObjectStore(document, table, new PdfObjectStoreOptions
            {
                MaximumCachedObjectStreamBytes = content.DecodedData.Length - 1,
            }).Resolve(new PdfIndirectReference(1)));
        Assert.Throws<PdfObjectResolutionException>(() =>
            new PdfObjectStore(document, table, new PdfObjectStoreOptions
            {
                StreamDecoderOptions = new PdfStreamDecoderOptions
                {
                    MaximumDecodedStreamLength = content.DecodedData.Length - 1,
                },
            }).Resolve(new PdfIndirectReference(1)));
    }

    [Fact]
    public void InvalidObjectStreamLimitsAreRejectedWhenTheStoreIsCreated()
    {
        var content = CreateContent((1, "null"));
        var document = CreateDocument(
            content.DecodedData,
            $"/Type /ObjStm /N 1 /First {content.FirstObjectOffset}",
            [new(1, 0)]);
        var table = new PdfCrossReferenceParser(document).Parse();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfObjectStore(document, table, new PdfObjectStoreOptions
            {
                MaximumObjectsPerObjectStream = -1,
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfObjectStore(document, table, new PdfObjectStoreOptions
            {
                MaximumCachedObjectStreams = -1,
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfObjectStore(document, table, new PdfObjectStoreOptions
            {
                MaximumCachedObjectStreamBytes = -1,
            }));
    }

    public static TheoryData<byte[], string, int> MalformedObjectStreams
    {
        get
        {
            var valid = CreateContent((1, "null"));
            return new TheoryData<byte[], string, int>
            {
                { valid.DecodedData, $"/N 1 /First {valid.FirstObjectOffset}", 0 },
                { valid.DecodedData, $"/Type /NotObjStm /N 1 /First {valid.FirstObjectOffset}", 0 },
                { valid.DecodedData, $"/Type /ObjStm /First {valid.FirstObjectOffset}", 0 },
                { valid.DecodedData, "/Type /ObjStm /N 1 /First 999", 0 },
                { "1 1 null"u8.ToArray(), "/Type /ObjStm /N 1 /First 4", 0 },
                { "1 0 1 5 null null"u8.ToArray(), "/Type /ObjStm /N 2 /First 8", 0 },
                { "1 0 2 0 null null"u8.ToArray(), "/Type /ObjStm /N 2 /First 8", 0 },
                { "1 0 extra null"u8.ToArray(), "/Type /ObjStm /N 1 /First 10", 0 },
                { valid.DecodedData, $"/Type /ObjStm /N 1 /First {valid.FirstObjectOffset}", 1 },
            };
        }
    }

    private static PdfObjectStore CreateStore(byte[] document)
    {
        var table = new PdfCrossReferenceParser(document).Parse();
        return new PdfObjectStore(document, table);
    }

    private static ObjectStreamContent CreateValidContent() => CreateContent(
        (1, "<< /Type /Catalog /Pages 2 0 R >>"),
        (2, "<< /Type /Pages /Kids [] /Count 0 >>"),
        (3, "(hello)"));

    private static ObjectStreamContent CreateContent(params (int ObjectNumber, string Value)[] objects)
    {
        var body = new StringBuilder();
        var header = new StringBuilder();
        foreach (var item in objects)
        {
            header.Append(item.ObjectNumber).Append(' ').Append(Encoding.ASCII.GetByteCount(body.ToString())).Append(' ');
            body.Append(item.Value).Append(' ');
        }

        var headerBytes = Encoding.ASCII.GetBytes(header.ToString());
        return new ObjectStreamContent(
            [.. headerBytes, .. Encoding.ASCII.GetBytes(body.ToString())],
            headerBytes.Length);
    }

    private static byte[] CreateDocument(
        byte[] decodedObjectStreamData,
        string objectStreamDictionaryEntries,
        IReadOnlyList<CompressedEntry> compressedEntries,
        bool useFlate = false,
        bool objectStreamIsCompressed = false,
        string xrefTrailerEntries = "")
    {
        const int objectStreamNumber = 5;
        const int xrefStreamNumber = 6;
        using var source = new MemoryStream();
        WriteAscii(source, "%PDF-1.7\n");
        var objectStreamOffset = (int)source.Position;
        var encodedObjectStreamData = useFlate
            ? Compress(decodedObjectStreamData)
            : decodedObjectStreamData;
        var filter = useFlate ? " /Filter /FlateDecode" : string.Empty;
        WriteAscii(
            source,
            $"5 0 obj\n<< {objectStreamDictionaryEntries} /Length {encodedObjectStreamData.Length}{filter} >>\nstream\n");
        source.Write(encodedObjectStreamData);
        WriteAscii(source, "\nendstream\nendobj\n");

        var xrefStreamOffset = (int)source.Position;
        var entries = Enumerable.Range(0, 7)
            .Select(_ => new XrefEntry(0, 0, 0))
            .ToArray();
        entries[0] = new XrefEntry(0, 0, ushort.MaxValue);
        foreach (var entry in compressedEntries)
        {
            entries[entry.ObjectNumber] = new XrefEntry(2, objectStreamNumber, (ulong)entry.Index);
        }

        entries[objectStreamNumber] = objectStreamIsCompressed
            ? new XrefEntry(2, objectStreamNumber, 0)
            : new XrefEntry(1, (ulong)objectStreamOffset, 0);
        entries[xrefStreamNumber] = new XrefEntry(1, (ulong)xrefStreamOffset, 0);
        var xrefData = EncodeEntries(entries);
        WriteAscii(
            source,
            $"6 0 obj\n<< /Type /XRef /Size 7 /W [1 4 2] /Root 1 0 R {xrefTrailerEntries} /Length {xrefData.Length} >>\nstream\n");
        source.Write(xrefData);
        WriteAscii(source, $"\nendstream\nendobj\nstartxref\n{xrefStreamOffset}\n%%EOF\n");
        return source.ToArray();
    }

    private static byte[] EncodeEntries(IEnumerable<XrefEntry> entries)
    {
        var output = new List<byte>();
        foreach (var entry in entries)
        {
            WriteField(output, entry.Type, 1);
            WriteField(output, entry.Field2, 4);
            WriteField(output, entry.Field3, 2);
        }

        return output.ToArray();
    }

    private static void WriteField(List<byte> output, ulong value, int width)
    {
        for (var shift = (width - 1) * 8; shift >= 0; shift -= 8)
        {
            output.Add((byte)(value >> shift));
        }
    }

    private static byte[] Compress(ReadOnlySpan<byte> input)
    {
        using var output = new MemoryStream();
        using (var compressor = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            compressor.Write(input);
        }

        return output.ToArray();
    }

    private static void WriteAscii(Stream output, string value) =>
        output.Write(Encoding.ASCII.GetBytes(value));

    private readonly record struct ObjectStreamContent(byte[] DecodedData, int FirstObjectOffset);

    private readonly record struct CompressedEntry(int ObjectNumber, int Index);

    private readonly record struct XrefEntry(ulong Type, ulong Field2, ulong Field3);
}
