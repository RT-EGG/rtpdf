using System.Text;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Parsing;
using Rtpdf.Core.Tokens;
using Xunit;

namespace Rtpdf.Core.Tests.Parsing;

public sealed class PdfObjectParserTests
{
    [Fact]
    public void ParseCreatesEveryPrimitiveObjectKind()
    {
        Assert.Same(PdfNull.Instance, Parse("null"));
        Assert.Equal(PdfBoolean.True, Parse("true"));
        Assert.Equal(PdfBoolean.False, Parse("false"));
        Assert.Equal(new PdfInteger(-42), Parse("-42"));
        Assert.Equal(new PdfReal(0.25m), Parse(".25"));
        Assert.Equal(new PdfName("A B"), Parse("/A#20B"));

        var literal = Assert.IsType<PdfString>(Parse("(A\\nB)"));
        Assert.Equal(PdfStringKind.Literal, literal.Kind);
        Assert.Equal("A\nB", literal.GetText());

        var hexadecimal = Assert.IsType<PdfString>(Parse("<4142>"));
        Assert.Equal(PdfStringKind.Hexadecimal, hexadecimal.Kind);
        Assert.Equal("AB", hexadecimal.GetText());
    }

    [Fact]
    public void ParseCreatesNestedArraysAndDictionaries()
    {
        var dictionary = Assert.IsType<PdfDictionary>(Parse(
            "<< /Type /Page /Count 2 /Kids [3 0 R 4 0 R] /Meta << /Enabled true >> >>"));

        Assert.Equal(new PdfName("Page"), dictionary["Type"]);
        Assert.Equal(new PdfInteger(2), dictionary["Count"]);

        var kids = Assert.IsType<PdfArray>(dictionary["Kids"]);
        Assert.Equal(new PdfIndirectReference(3), kids[0]);
        Assert.Equal(new PdfIndirectReference(4), kids[1]);

        var metadata = Assert.IsType<PdfDictionary>(dictionary["Meta"]);
        Assert.Equal(PdfBoolean.True, metadata["Enabled"]);
    }

    [Fact]
    public void ParseCreatesIndirectReferenceAndValidatesItsNumbers()
    {
        Assert.Equal(new PdfIndirectReference(12, 3), Parse("12 3 R"));

        var objectNumber = Assert.Throws<PdfParseException>(() => Parse("0 0 R"));
        Assert.Equal(0, objectNumber.Offset);

        var generation = Assert.Throws<PdfParseException>(() => Parse("1 65536 R"));
        Assert.Equal(2, generation.Offset);
    }

    [Fact]
    public void ReadObjectCanReadASequenceWithoutRequiringEndOfInput()
    {
        var parser = CreateParser("1 /Two [3]");

        Assert.Equal(new PdfInteger(1), parser.ReadObject());
        Assert.Equal(new PdfName("Two"), parser.ReadObject());
        Assert.Single(Assert.IsType<PdfArray>(parser.ReadObject()));
        Assert.True(parser.IsAtEnd);
    }

    [Fact]
    public void ParseRejectsTrailingTokens()
    {
        var exception = Assert.Throws<PdfParseException>(() => Parse("1 2"));

        Assert.Equal(2, exception.Offset);
        Assert.Equal(PdfTokenKind.Integer, exception.TokenKind);
    }

    [Theory]
    [InlineData("obj")]
    [InlineData("]")]
    [InlineData(">>")]
    [InlineData("{")]
    public void ParseRejectsTokensThatCannotStartAnObject(string input)
    {
        Assert.Throws<PdfParseException>(() => Parse(input));
    }

    [Theory]
    [InlineData("[")]
    [InlineData("[1 2")]
    [InlineData("<<")]
    [InlineData("<< /A 1")]
    public void ParseRejectsUnterminatedContainers(string input)
    {
        Assert.Throws<PdfParseException>(() => Parse(input));
    }

    [Theory]
    [InlineData("<< 1 /Value >>", 3)]
    [InlineData("<< /Key >>", 8)]
    public void ParseRejectsMalformedDictionaryEntries(string input, int expectedOffset)
    {
        var exception = Assert.Throws<PdfParseException>(() => Parse(input));

        Assert.Equal(expectedOffset, exception.Offset);
    }

    [Fact]
    public void ParseRejectsDuplicateDictionaryKeysAfterNameDecoding()
    {
        var exception = Assert.Throws<PdfParseException>(() => Parse("<< /A 1 /#41 2 >>"));

        Assert.Contains("Duplicate", exception.Message);
        Assert.Equal(8, exception.Offset);
    }

    [Theory]
    [InlineData("999999999999999999999999999999")]
    [InlineData("999999999999999999999999999999.0")]
    public void ParseRejectsNumbersOutsideTheObjectModelsRange(string input)
    {
        Assert.Throws<PdfParseException>(() => Parse(input));
    }

    [Fact]
    public void MaximumNestingDepthStopsDeepContainers()
    {
        var parser = CreateParser(
            "[[]]",
            new PdfObjectParserOptions { MaximumNestingDepth = 1 });

        var exception = Assert.Throws<PdfParseException>(() => parser.Parse());
        Assert.Equal(1, exception.Offset);
    }

    [Fact]
    public void MaximumArrayItemsIsEnforcedBeforeParsingTheExcessItem()
    {
        var parser = CreateParser(
            "[1 2]",
            new PdfObjectParserOptions { MaximumArrayItems = 1 });

        var exception = Assert.Throws<PdfParseException>(() => parser.Parse());
        Assert.Equal(3, exception.Offset);
    }

    [Fact]
    public void MaximumDictionaryEntriesIsEnforcedBeforeParsingTheExcessEntry()
    {
        var parser = CreateParser(
            "<< /A 1 /B 2 >>",
            new PdfObjectParserOptions { MaximumDictionaryEntries = 1 });

        var exception = Assert.Throws<PdfParseException>(() => parser.Parse());
        Assert.Equal(8, exception.Offset);
    }

    [Fact]
    public void InvalidResourceLimitIsRejectedAtConstruction()
    {
        var options = new PdfObjectParserOptions { MaximumNestingDepth = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateParser("null", options));
    }

    [Fact]
    public void EmptyInputReportsTheEndOffset()
    {
        var exception = Assert.Throws<PdfParseException>(() => Parse("  "));

        Assert.Equal(2, exception.Offset);
        Assert.Equal(PdfTokenKind.EndOfInput, exception.TokenKind);
    }

    [Fact]
    public void StreamInputIsParsedFromItsCurrentPositionAndLeftOpen()
    {
        using var stream = new MemoryStream("skip [1]"u8.ToArray());
        stream.Position = 5;
        var parser = new PdfObjectParser(stream);

        Assert.Single(Assert.IsType<PdfArray>(parser.Parse()));
        Assert.True(stream.CanRead);
    }

    private static PdfObject Parse(string input) => CreateParser(input).Parse();

    private static PdfObjectParser CreateParser(string input, PdfObjectParserOptions? options = null) =>
        new(Encoding.Latin1.GetBytes(input), options);
}
