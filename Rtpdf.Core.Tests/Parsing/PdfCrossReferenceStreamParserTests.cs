using System.IO.Compression;
using System.Text;
using Rtpdf.Core.CrossReferences;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Documents;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Parsing;
using Rtpdf.Core.Storage;
using Xunit;

namespace Rtpdf.Core.Tests.Parsing;

public sealed class PdfCrossReferenceStreamParserTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParseReadsPlainAndFlateEncodedCrossReferenceStreams(bool useFlate)
    {
        const string prefix = "%PDF-1.7\n1 0 obj\n(plain)\nendobj\n";
        var objectOffset = prefix.IndexOf("1 0 obj", StringComparison.Ordinal);
        var xrefOffset = Encoding.ASCII.GetByteCount(prefix);
        var data = EncodeEntries(
            [1, 4, 2],
            new Entry(0, 0, ushort.MaxValue),
            new Entry(1, (ulong)objectOffset, 0),
            new Entry(1, (ulong)xrefOffset, 0));
        var document = CreateXrefStreamDocument(
            prefix,
            5,
            "/Type /XRef /Size 6 /W [1 4 2] /Index [0 2 5 1] /Root 1 0 R",
            data,
            useFlate);

        var table = new PdfCrossReferenceParser(document.Bytes).Parse();

        var section = Assert.Single(table.Sections);
        Assert.Equal(PdfCrossReferenceSectionKind.Stream, section.Kind);
        Assert.Equal(new PdfObjectId(5), section.CrossReferenceStreamObjectId);
        Assert.Equal(xrefOffset, table.CrossReferenceOffset);
        Assert.Equal(3, table.Count);
        Assert.Equal(objectOffset, table[1].ByteOffset);
        Assert.Equal(xrefOffset, table[5].ByteOffset);
        Assert.Equal(new PdfIndirectReference(1), table.Trailer["Root"]);

        var store = new PdfObjectStore(document.Bytes, table);
        Assert.Equal(
            "plain",
            Assert.IsType<PdfString>(store.GetIndirectObject(new PdfObjectId(1)).Value).GetText());
    }

    [Fact]
    public void MissingIndexDefaultsToTheRangeFromZeroToSize()
    {
        const string prefix = "%PDF-1.7\n";
        var xrefOffset = Encoding.ASCII.GetByteCount(prefix);
        var data = EncodeEntries(
            [1, 4, 2],
            new Entry(0, 0, ushort.MaxValue),
            new Entry(1, (ulong)xrefOffset, 0));
        var document = CreateXrefStreamDocument(
            prefix,
            1,
            "/Type /XRef /Size 2 /W [1 4 2]",
            data);

        var table = new PdfCrossReferenceParser(document.Bytes).Parse();

        Assert.Equal(new[] { 0, 1 }, table.Entries.Select(entry => entry.ObjectNumber));
    }

    [Fact]
    public void ZeroWidthTypeFieldDefaultsToInUseAndSupportsCatalogReading()
    {
        const string prefix =
            "%PDF-1.7\n" +
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
            "2 0 obj\n<< /Type /Pages /Kids [] /Count 0 >>\nendobj\n";
        var catalogOffset = prefix.IndexOf("1 0 obj", StringComparison.Ordinal);
        var pagesOffset = prefix.IndexOf("2 0 obj", StringComparison.Ordinal);
        var xrefOffset = Encoding.ASCII.GetByteCount(prefix);
        var data = EncodeEntries(
            [0, 4, 2],
            new Entry(1, (ulong)catalogOffset, 0),
            new Entry(1, (ulong)pagesOffset, 0),
            new Entry(1, (ulong)xrefOffset, 0));
        var document = CreateXrefStreamDocument(
            prefix,
            3,
            "/Type /XRef /Size 4 /W [0 4 2] /Index [1 3] /Root 1 0 R",
            data);

        var table = new PdfCrossReferenceParser(document.Bytes).Parse();
        var store = new PdfObjectStore(document.Bytes, table);
        var catalog = new PdfCatalogReader(table, store).Read();

        Assert.All(table.Entries, entry => Assert.Equal(PdfObjectLocationKind.InUse, entry.Kind));
        Assert.Equal(new PdfObjectId(1), catalog.ObjectId);
        Assert.Empty(catalog.PageTree.Pages);
    }

    [Fact]
    public void TypeTwoEntryExposesObjectStreamLocation()
    {
        const string prefix = "%PDF-1.7\n";
        var xrefOffset = Encoding.ASCII.GetByteCount(prefix);
        var data = EncodeEntries(
            [1, 2, 2],
            new Entry(2, 3, 7),
            new Entry(1, (ulong)xrefOffset, 0));
        var document = CreateXrefStreamDocument(
            prefix,
            5,
            "/Type /XRef /Size 6 /W [1 2 2] /Index [2 1 5 1]",
            data);

        var table = new PdfCrossReferenceParser(document.Bytes).Parse();

        var compressed = table[2];
        Assert.Equal(PdfObjectLocationKind.Compressed, compressed.Kind);
        Assert.True(compressed.IsInUse);
        Assert.Null(compressed.ByteOffset);
        Assert.Equal(3, compressed.ObjectStreamNumber);
        Assert.Equal(7, compressed.ObjectStreamIndex);
        Assert.Equal((ushort)0, compressed.GenerationNumber);

    }

    [Fact]
    public void IncrementalUpdateCanMixAnXrefStreamWithAnOlderConventionalTable()
    {
        using var source = new MemoryStream();
        WriteAscii(source, "%PDF-1.7\n");
        var oldObjectOffset = (int)source.Position;
        WriteAscii(source, "1 0 obj\n(old)\nendobj\n");
        var baseXrefOffset = (int)source.Position;
        WriteAscii(
            source,
            $"xref\n0 2\n0000000000 65535 f \n{oldObjectOffset:D10} 00000 n \n" +
            $"trailer\n<< /Size 2 /Root 1 0 R >>\nstartxref\n{baseXrefOffset}\n%%EOF\n");

        var updatedObjectOffset = (int)source.Position;
        WriteAscii(source, "1 0 obj\n(updated)\nendobj\n");
        var streamOffset = (int)source.Position;
        var data = EncodeEntries(
            [1, 4, 2],
            new Entry(1, (ulong)updatedObjectOffset, 0),
            new Entry(1, (ulong)streamOffset, 0));
        WriteXrefStreamObject(
            source,
            2,
            $"/Type /XRef /Size 3 /W [1 4 2] /Index [1 2] /Root 1 0 R /Prev {baseXrefOffset}",
            data,
            useFlate: false);
        WriteAscii(source, $"startxref\n{streamOffset}\n%%EOF\n");
        var bytes = source.ToArray();

        var table = new PdfCrossReferenceParser(bytes).Parse();

        Assert.Equal(
            new[] { PdfCrossReferenceSectionKind.Stream, PdfCrossReferenceSectionKind.ConventionalTable },
            table.Sections.Select(section => section.Kind));
        Assert.Equal(updatedObjectOffset, table[1].ByteOffset);
        Assert.Equal(
            "updated",
            Assert.IsType<PdfString>(
                new PdfObjectStore(bytes, table).GetIndirectObject(new PdfObjectId(1)).Value).GetText());
    }

    [Fact]
    public void HybridReferenceLoadsXRefStmAfterTheConventionalSection()
    {
        using var source = new MemoryStream();
        WriteAscii(source, "%PDF-1.7\n");
        var objectOffset = (int)source.Position;
        WriteAscii(source, "1 0 obj\n(visible)\nendobj\n");

        var streamOffset = (int)source.Position;
        var streamData = EncodeEntries(
            [1, 4, 2],
            new Entry(1, (ulong)streamOffset, 0),
            new Entry(2, 3, 7),
            new Entry(1, (ulong)streamOffset, 0));
        WriteXrefStreamObject(
            source,
            4,
            "/Type /XRef /Size 5 /W [1 4 2] /Index [1 2 4 1]",
            streamData,
            useFlate: false);

        var tableOffset = (int)source.Position;
        WriteAscii(
            source,
            $"xref\n0 2\n0000000000 65535 f \n{objectOffset:D10} 00000 n \n" +
            $"trailer\n<< /Size 5 /Root 1 0 R /XRefStm {streamOffset} >>\n" +
            $"startxref\n{tableOffset}\n%%EOF\n");

        var table = new PdfCrossReferenceParser(source.ToArray()).Parse();

        Assert.Equal(
            new[] { PdfCrossReferenceSectionKind.ConventionalTable, PdfCrossReferenceSectionKind.Stream },
            table.Sections.Select(section => section.Kind));
        Assert.Equal(objectOffset, table[1].ByteOffset);
        Assert.Equal(PdfObjectLocationKind.Compressed, table[2].Kind);
        Assert.Equal(3, table[2].ObjectStreamNumber);
    }

    [Fact]
    public void UnknownEntryTypesAreTreatedAsMissingEntries()
    {
        const string prefix = "%PDF-1.7\n";
        var xrefOffset = Encoding.ASCII.GetByteCount(prefix);
        var data = EncodeEntries(
            [1, 2, 1],
            new Entry(3, 0, 0),
            new Entry(1, (ulong)xrefOffset, 0));
        var document = CreateXrefStreamDocument(
            prefix,
            5,
            "/Type /XRef /Size 6 /W [1 2 1] /Index [2 1 5 1]",
            data);

        var table = new PdfCrossReferenceParser(document.Bytes).Parse();

        Assert.Single(table.Entries);
        Assert.False(table.TryGetLocation(2, out _));
    }

    [Fact]
    public void ParseRejectsXrefStreamWithoutAnEntryForItself()
    {
        var document = CreateXrefStreamDocument(
            "%PDF-1.7\n",
            5,
            "/Type /XRef /Size 6 /W [1 1 1] /Index [2 1]",
            [1, 0, 0]);

        var exception = Assert.Throws<PdfCrossReferenceException>(() =>
            new PdfCrossReferenceParser(document.Bytes).Parse());

        Assert.Contains("matching in-use", exception.Message);
    }

    [Theory]
    [MemberData(nameof(MalformedStreams))]
    public void ParseRejectsMalformedCrossReferenceStreams(string dictionaryEntries, byte[] data)
    {
        var document = CreateXrefStreamDocument("%PDF-1.7\n", 5, dictionaryEntries, data);

        Assert.Throws<PdfCrossReferenceException>(() =>
            new PdfCrossReferenceParser(document.Bytes).Parse());
    }

    [Fact]
    public void MaximumEntriesAppliesWhenIndexIsOmitted()
    {
        var document = CreateXrefStreamDocument(
            "%PDF-1.7\n",
            1,
            "/Type /XRef /Size 2 /W [1 1 1]",
            [0, 0, 0, 1, 0, 0]);
        var options = new PdfCrossReferenceParserOptions { MaximumEntries = 1 };

        Assert.Throws<PdfCrossReferenceException>(() =>
            new PdfCrossReferenceParser(document.Bytes, options).Parse());
    }

    public static TheoryData<string, byte[]> MalformedStreams => new()
    {
        { "/Size 1 /W [1 1 1] /Index [0 1]", new byte[] { 0, 0, 0 } },
        { "/Type /XRef /Size 1 /W [0 0 0] /Index [0 1]", Array.Empty<byte>() },
        { "/Type /XRef /Size 2 /W [1 1 1] /Index [0]", new byte[] { 0, 0, 0 } },
        { "/Type /XRef /Size 3 /W [1 1 1] /Index [0 2 1 1]", new byte[9] },
        { "/Type /XRef /Size 1 /W [1 1 1] /Index [0 1]", new byte[] { 0, 0 } },
        { "/Type /XRef /Size 1 /W [1 1 3] /Index [0 1]", new byte[] { 0, 0, 1, 0, 0 } },
    };

    private static TestDocument CreateXrefStreamDocument(
        string prefix,
        int objectNumber,
        string dictionaryEntries,
        byte[] decodedData,
        bool useFlate = false)
    {
        using var source = new MemoryStream();
        WriteAscii(source, prefix);
        var xrefOffset = (int)source.Position;
        WriteXrefStreamObject(source, objectNumber, dictionaryEntries, decodedData, useFlate);
        WriteAscii(source, $"startxref\n{xrefOffset}\n%%EOF\n");
        return new TestDocument(source.ToArray(), xrefOffset);
    }

    private static void WriteXrefStreamObject(
        Stream output,
        int objectNumber,
        string dictionaryEntries,
        byte[] decodedData,
        bool useFlate)
    {
        var encodedData = useFlate ? Compress(decodedData) : decodedData;
        var filter = useFlate ? " /Filter /FlateDecode" : string.Empty;
        WriteAscii(
            output,
            $"{objectNumber} 0 obj\n<< {dictionaryEntries} /Length {encodedData.Length}{filter} >>\nstream\n");
        output.Write(encodedData);
        WriteAscii(output, "\nendstream\nendobj\n");
    }

    private static byte[] EncodeEntries(IReadOnlyList<int> widths, params Entry[] entries)
    {
        var output = new List<byte>();
        foreach (var entry in entries)
        {
            WriteField(output, entry.Type, widths[0]);
            WriteField(output, entry.Field2, widths[1]);
            WriteField(output, entry.Field3, widths[2]);
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

    private readonly record struct Entry(ulong Type, ulong Field2, ulong Field3);

    private sealed record TestDocument(byte[] Bytes, int CrossReferenceOffset);
}
