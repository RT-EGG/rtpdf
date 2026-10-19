using System.Text;
using Rtpdf.Core.CrossReferences;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Parsing;
using Rtpdf.Core.Storage;
using Xunit;

namespace Rtpdf.Core.Tests.Parsing;

public sealed class PdfCrossReferenceParserTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void ParseReadsConventionalTableTrailerAndStartXref(string lineEnding)
    {
        var document = CreateDocument(lineEnding);

        var table = new PdfCrossReferenceParser(document.Bytes).Parse();

        Assert.Equal(document.CrossReferenceOffset, table.CrossReferenceOffset);
        Assert.True(table.StartXrefKeywordOffset > table.TrailerOffset);
        Assert.Equal(2, table.DeclaredSize);
        Assert.Equal(2, table.Count);

        var free = table[0];
        Assert.Equal(PdfObjectLocationKind.Free, free.Kind);
        Assert.Equal(0, free.NextFreeObjectNumber);
        Assert.Null(free.ByteOffset);
        Assert.Equal(ushort.MaxValue, free.GenerationNumber);

        var catalog = table[1];
        Assert.Equal(PdfObjectLocationKind.InUse, catalog.Kind);
        Assert.Equal(document.ObjectOffset, catalog.ByteOffset);
        Assert.Null(catalog.NextFreeObjectNumber);
        Assert.Equal((ushort)0, catalog.GenerationNumber);

        Assert.Equal(new PdfIndirectReference(1), table.Trailer["Root"]);
        Assert.Equal("kept", Assert.IsType<PdfString>(table.Trailer["Custom"]).GetText());
    }

    [Fact]
    public void ParseReadsMultipleSparseSubsectionsInObjectNumberOrder()
    {
        const string input = "paddingxxxxxxxxxxxxxref\n0 1\n0 65535 f\n5 2\n1 0 n\n2 3 n\ntrailer\n<< /Size 7 >>";
        var bytes = Encoding.ASCII.GetBytes(input);
        var offset = input.IndexOf("xref", StringComparison.Ordinal);

        var table = new PdfCrossReferenceParser(bytes).ParseAt(offset);

        Assert.Equal(new[] { 0, 5, 6 }, table.Entries.Select(entry => entry.ObjectNumber));
        Assert.Equal(1, table[5].ByteOffset);
        Assert.Equal((ushort)3, table[6].GenerationNumber);
        Assert.Equal(-1, table.StartXrefKeywordOffset);
        Assert.True(table.TryGetLocation(6, out _));
        Assert.False(table.TryGetLocation(1, out _));
    }

    [Fact]
    public void ParseAtDoesNotRequireStartXrefOrEndOfFileMarker()
    {
        var bytes = "xxxxxref\n0 1\n0 65535 f\ntrailer\n<< /Size 1 >>"u8.ToArray();

        var table = new PdfCrossReferenceParser(bytes).ParseAt(4);

        Assert.Single(table.Entries);
        Assert.Equal(4, table.CrossReferenceOffset);
    }

    [Fact]
    public void StreamInputStartsAtCurrentPositionAndRemainsOpen()
    {
        var tableBytes = "xref\n0 1\n0 65535 f\ntrailer\n<< /Size 1 >>"u8.ToArray();
        using var stream = new MemoryStream([1, 2, 3, .. tableBytes]);
        stream.Position = 3;

        var table = new PdfCrossReferenceParser(stream).ParseAt(0);

        Assert.Single(table.Entries);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void ParseFollowsPreviousSectionsAndBuildsNewestEffectiveView()
    {
        var document = CreateIncrementalDocument();

        var table = new PdfCrossReferenceParser(document.Bytes).Parse();

        Assert.Equal(document.LatestCrossReferenceOffset, table.CrossReferenceOffset);
        Assert.Equal(document.BaseCrossReferenceOffset, table.PreviousCrossReferenceOffset);
        Assert.Equal(5, table.DeclaredSize);
        Assert.Equal(5, table.Count);
        Assert.Equal(
            new long[] { document.LatestCrossReferenceOffset, document.BaseCrossReferenceOffset },
            table.Sections.Select(section => section.CrossReferenceOffset));

        Assert.Equal(document.UpdatedObjectOffset, table[1].ByteOffset);
        Assert.Equal(PdfObjectLocationKind.Free, table[2].Kind);
        Assert.Equal((ushort)1, table[2].GenerationNumber);
        Assert.Equal(document.KeptObjectOffset, table[3].ByteOffset);
        Assert.Equal(document.NewObjectOffset, table[4].ByteOffset);
        Assert.Equal("latest", Assert.IsType<PdfString>(table.Trailer["Custom"]).GetText());
    }

    [Fact]
    public void EffectiveTableResolvesUpdatedAndUnchangedObjectsAndHonorsDeletion()
    {
        var document = CreateIncrementalDocument();
        var table = new PdfCrossReferenceParser(document.Bytes).Parse();
        var store = new PdfObjectStore(document.Bytes, table);

        Assert.Equal("updated", Assert.IsType<PdfString>(
            store.GetIndirectObject(new PdfObjectId(1)).Value).GetText());
        Assert.Equal("kept", Assert.IsType<PdfString>(
            store.GetIndirectObject(new PdfObjectId(3)).Value).GetText());
        Assert.Equal("new", Assert.IsType<PdfString>(
            store.GetIndirectObject(new PdfObjectId(4)).Value).GetText());
        Assert.Throws<PdfObjectResolutionException>(() =>
            store.GetIndirectObject(new PdfObjectId(2, 1)));
    }

    [Fact]
    public void ParseAtReadsOnlyTheRequestedPhysicalSection()
    {
        var document = CreateIncrementalDocument();

        var table = new PdfCrossReferenceParser(document.Bytes)
            .ParseAt(document.LatestCrossReferenceOffset);

        Assert.Single(table.Sections);
        Assert.Equal(new[] { 1, 2, 4 }, table.Entries.Select(entry => entry.ObjectNumber));
        Assert.Equal(document.BaseCrossReferenceOffset, table.PreviousCrossReferenceOffset);
        Assert.False(table.TryGetLocation(3, out _));
    }

    [Fact]
    public void ParseRejectsCircularPreviousSectionChain()
    {
        var firstTemplate = "xref\n0 1\n0 65535 f\ntrailer\n<< /Size 1 /Prev {0:D10} >>\n";
        var firstLength = Encoding.ASCII.GetByteCount(string.Format(firstTemplate, 0));
        var first = string.Format(firstTemplate, firstLength);
        var second = $"xref\n0 1\n0 65535 f\ntrailer\n<< /Size 1 /Prev 0 >>\nstartxref\n{firstLength}\n%%EOF\n";

        var exception = Assert.Throws<PdfCrossReferenceException>(() =>
            new PdfCrossReferenceParser(Encoding.ASCII.GetBytes(first + second)).Parse());

        Assert.Contains("Circular", exception.Message);
    }

    [Fact]
    public void MaximumCrossReferenceSectionsIsEnforced()
    {
        var document = CreateIncrementalDocument();
        var options = new PdfCrossReferenceParserOptions { MaximumCrossReferenceSections = 1 };

        var exception = Assert.Throws<PdfCrossReferenceException>(() =>
            new PdfCrossReferenceParser(document.Bytes, options).Parse());

        Assert.Contains("1 sections", exception.Message);
    }

    [Fact]
    public void MaximumEntriesIsAlsoEnforcedAcrossMergedSections()
    {
        var document = CreateIncrementalDocument();
        var options = new PdfCrossReferenceParserOptions { MaximumEntries = 4 };

        var exception = Assert.Throws<PdfCrossReferenceException>(() =>
            new PdfCrossReferenceParser(document.Bytes, options).Parse());

        Assert.Contains("Effective xref table", exception.Message);
    }

    [Fact]
    public void CommentsAndVariableWidthFieldsAreAccepted()
    {
        const string input = "xref % section\n0 2 % header\n0 65535 f % free\n1 0 n\ntrailer\n<< /Size 2 >>";

        var table = new PdfCrossReferenceParser(Encoding.ASCII.GetBytes(input)).ParseAt(0);

        Assert.Equal(2, table.Count);
        Assert.Equal(1, table[1].ByteOffset);
    }

    [Theory]
    [InlineData("no cross reference here")]
    [InlineData("startxref\n999\n%%EOF\n")]
    public void ParseRejectsMissingOrOutOfRangeStartXref(string input)
    {
        Assert.Throws<PdfCrossReferenceException>(() =>
            new PdfCrossReferenceParser(Encoding.ASCII.GetBytes(input)).Parse());
    }

    [Fact]
    public void ParseRejectsMissingEndOfFileMarkerByDefault()
    {
        var document = CreateDocument("\n");
        var truncated = document.Bytes.AsSpan(0, document.Bytes.Length - "%%EOF\n"u8.Length).ToArray();

        Assert.Throws<PdfCrossReferenceException>(() => new PdfCrossReferenceParser(truncated).Parse());
    }

    [Fact]
    public void EndOfFileMarkerValidationCanBeDisabled()
    {
        var document = CreateDocument("\n");
        var truncated = document.Bytes.AsSpan(0, document.Bytes.Length - "%%EOF\n"u8.Length).ToArray();
        var options = new PdfCrossReferenceParserOptions { RequireEndOfFileMarker = false };

        var table = new PdfCrossReferenceParser(truncated, options).Parse();

        Assert.Equal(2, table.Count);
    }

    [Fact]
    public void ParseRejectsUnexpectedDataAfterEndOfFileMarker()
    {
        var document = CreateDocument("\n");
        byte[] bytes = [.. document.Bytes, .. "garbage"u8.ToArray()];

        Assert.Throws<PdfCrossReferenceException>(() => new PdfCrossReferenceParser(bytes).Parse());
    }

    [Fact]
    public void StartXrefSearchWindowIsEnforced()
    {
        var document = CreateDocument("\n");
        var options = new PdfCrossReferenceParserOptions { MaximumStartXrefSearchBytes = 8 };

        Assert.Throws<PdfCrossReferenceException>(() =>
            new PdfCrossReferenceParser(document.Bytes, options).Parse());
    }

    [Theory]
    [InlineData("xref\n0 1\n999999 0 n\ntrailer\n<< /Size 1 >>")]
    [InlineData("xref\n0 1\n0 65536 f\ntrailer\n<< /Size 1 >>")]
    [InlineData("xref\n0 1\n0 0 z\ntrailer\n<< /Size 1 >>")]
    public void ParseAtRejectsMalformedEntries(string input)
    {
        Assert.Throws<PdfCrossReferenceException>(() =>
            new PdfCrossReferenceParser(Encoding.ASCII.GetBytes(input)).ParseAt(0));
    }

    [Fact]
    public void ParseAtRejectsDuplicateEntriesAcrossSubsections()
    {
        const string input = "xref\n0 1\n0 65535 f\n0 1\n0 65535 f\ntrailer\n<< /Size 1 >>";

        var exception = Assert.Throws<PdfCrossReferenceException>(() =>
            new PdfCrossReferenceParser(Encoding.ASCII.GetBytes(input)).ParseAt(0));

        Assert.Contains("Duplicate", exception.Message);
    }

    [Theory]
    [InlineData("xref\n0 1\n0 65535 f\ntrailer\n<< >>")]
    [InlineData("xref\n0 1\n0 65535 f\ntrailer\n<< /Size /One >>")]
    [InlineData("xref\n0 2\n0 65535 f\n1 0 n\ntrailer\n<< /Size 1 >>")]
    public void ParseAtValidatesTrailerSize(string input)
    {
        Assert.Throws<PdfCrossReferenceException>(() =>
            new PdfCrossReferenceParser(Encoding.ASCII.GetBytes(input)).ParseAt(0));
    }

    [Fact]
    public void ParseRejectsSelfReferencingPreviousTable()
    {
        const string input = "xref\n0 1\n0 65535 f\ntrailer\n<< /Size 1 /Prev 0 >>";

        Assert.Throws<PdfCrossReferenceException>(() =>
            new PdfCrossReferenceParser(Encoding.ASCII.GetBytes(input)).ParseAt(0));
    }

    [Fact]
    public void MaximumEntriesIsEnforcedBeforeReadingEntries()
    {
        const string input = "xref\n0 2\n0 65535 f\n1 0 n\ntrailer\n<< /Size 2 >>";
        var options = new PdfCrossReferenceParserOptions { MaximumEntries = 1 };

        Assert.Throws<PdfCrossReferenceException>(() =>
            new PdfCrossReferenceParser(Encoding.ASCII.GetBytes(input), options).ParseAt(0));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, 0)]
    public void InvalidOptionsAreRejected(int maximumEntries, int searchBytes)
    {
        var options = new PdfCrossReferenceParserOptions
        {
            MaximumEntries = maximumEntries,
            MaximumStartXrefSearchBytes = searchBytes,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfCrossReferenceParser(ReadOnlyMemory<byte>.Empty, options));
    }

    [Fact]
    public void ZeroMaximumCrossReferenceSectionsIsRejected()
    {
        var options = new PdfCrossReferenceParserOptions { MaximumCrossReferenceSections = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfCrossReferenceParser(ReadOnlyMemory<byte>.Empty, options));
    }

    private static TestDocument CreateDocument(string lineEnding)
    {
        var header = $"%PDF-1.7{lineEnding}1 0 obj{lineEnding}<< /Type /Catalog >>{lineEnding}endobj{lineEnding}";
        var objectOffset = header.IndexOf("1 0 obj", StringComparison.Ordinal);
        var crossReferenceOffset = Encoding.ASCII.GetByteCount(header);
        var tail = string.Join(
            lineEnding,
            "xref",
            "0 2",
            "0000000000 65535 f ",
            $"{objectOffset:D10} 00000 n ",
            "trailer",
            "<< /Size 2 /Root 1 0 R /Custom (kept) >>",
            "startxref",
            crossReferenceOffset.ToString(),
            "%%EOF",
            string.Empty);

        return new TestDocument(Encoding.ASCII.GetBytes(header + tail), objectOffset, crossReferenceOffset);
    }

    private static IncrementalTestDocument CreateIncrementalDocument()
    {
        var source = new StringBuilder("%PDF-1.7\n");
        var oldObjectOffset = source.Length;
        source.Append("1 0 obj\n(old)\nendobj\n");
        var deletedObjectOffset = source.Length;
        source.Append("2 0 obj\n(deleted)\nendobj\n");
        var keptObjectOffset = source.Length;
        source.Append("3 0 obj\n(kept)\nendobj\n");

        var baseCrossReferenceOffset = source.Length;
        source.Append("xref\n0 4\n")
            .Append("0000000000 65535 f \n")
            .AppendFormat("{0:D10} 00000 n \n", oldObjectOffset)
            .AppendFormat("{0:D10} 00000 n \n", deletedObjectOffset)
            .AppendFormat("{0:D10} 00000 n \n", keptObjectOffset)
            .Append("trailer\n<< /Size 4 /Root 1 0 R /Custom (base) >>\n")
            .Append("startxref\n")
            .Append(baseCrossReferenceOffset)
            .Append("\n%%EOF\n");

        var updatedObjectOffset = source.Length;
        source.Append("1 0 obj\n(updated)\nendobj\n");
        var newObjectOffset = source.Length;
        source.Append("4 0 obj\n(new)\nendobj\n");

        var latestCrossReferenceOffset = source.Length;
        source.Append("xref\n1 2\n")
            .AppendFormat("{0:D10} 00000 n \n", updatedObjectOffset)
            .Append("0000000000 00001 f \n")
            .Append("4 1\n")
            .AppendFormat("{0:D10} 00000 n \n", newObjectOffset)
            .Append("trailer\n<< /Size 5 /Root 1 0 R /Prev ")
            .Append(baseCrossReferenceOffset)
            .Append(" /Custom (latest) >>\nstartxref\n")
            .Append(latestCrossReferenceOffset)
            .Append("\n%%EOF\n");

        return new IncrementalTestDocument(
            Encoding.ASCII.GetBytes(source.ToString()),
            baseCrossReferenceOffset,
            latestCrossReferenceOffset,
            updatedObjectOffset,
            keptObjectOffset,
            newObjectOffset);
    }

    private sealed record TestDocument(byte[] Bytes, int ObjectOffset, int CrossReferenceOffset);

    private sealed record IncrementalTestDocument(
        byte[] Bytes,
        int BaseCrossReferenceOffset,
        int LatestCrossReferenceOffset,
        int UpdatedObjectOffset,
        int KeptObjectOffset,
        int NewObjectOffset);
}
