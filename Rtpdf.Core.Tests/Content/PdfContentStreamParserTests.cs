using System.IO.Compression;
using System.Text;
using Rtpdf.Core.Content;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Filters;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Parsing;
using Xunit;

namespace Rtpdf.Core.Tests.Content;

public sealed class PdfContentStreamParserTests
{
    [Fact]
    public void ParseGroupsOperandsWithTheirFollowingOperators()
    {
        var operations = Parse("q 1 0 0 1 10 20 cm /Im0 Do Q");

        Assert.Collection(
            operations,
            operation => AssertOperation(operation, "q", 0, 0),
            operation =>
            {
                AssertOperation(operation, "cm", 6, 16);
                Assert.Equal(new PdfInteger(10), operation.Operands[4]);
                Assert.Equal(new PdfInteger(20), operation.Operands[5]);
            },
            operation =>
            {
                AssertOperation(operation, "Do", 1, 24);
                Assert.Equal(new PdfName("Im0"), operation.Operands[0]);
            },
            operation => AssertOperation(operation, "Q", 0, 27));
    }

    [Fact]
    public void ParseSupportsCompositeDirectObjectsCommentsAndCustomOperators()
    {
        var operations = Parse("% comment\n[(A) <42> 3] << /Enabled true >> VendorOp");

        var operation = Assert.Single(operations);
        AssertOperation(operation, "VendorOp", 2, 43);

        var array = Assert.IsType<PdfArray>(operation.Operands[0]);
        Assert.Equal(3, array.Count);
        Assert.Equal("A", Assert.IsType<PdfString>(array[0]).GetText());
        Assert.Equal("B", Assert.IsType<PdfString>(array[1]).GetText());

        var dictionary = Assert.IsType<PdfDictionary>(operation.Operands[1]);
        Assert.Equal(PdfBoolean.True, dictionary["Enabled"]);
    }

    [Fact]
    public void ParseReturnsAnEmptyListForEmptyContent()
    {
        var operations = Parse(" \r\n% nothing follows");

        Assert.Empty(operations);
    }

    [Fact]
    public void StreamInputStartsAtCurrentPositionAndIsLeftOpen()
    {
        using var input = new MemoryStream("skip 12 w"u8.ToArray());
        input.Position = 5;

        var operations = new PdfContentStreamParser(input).Parse();

        Assert.Single(operations);
        Assert.Equal(new PdfInteger(12), operations[0].Operands[0]);
        Assert.True(input.CanRead);
    }

    [Fact]
    public void PdfStreamInputIsDecodedBeforeParsing()
    {
        var dictionary = new PdfDictionary
        {
            ["Filter"] = new PdfName("FlateDecode"),
        };
        var stream = new PdfStream(dictionary, Compress("2 J"u8));

        var operations = new PdfContentStreamParser(stream, new PdfStreamDecoder()).Parse();

        var operation = Assert.Single(operations);
        AssertOperation(operation, "J", 1, 2);
        Assert.Equal(new PdfInteger(2), operation.Operands[0]);
    }

    [Fact]
    public void ParseRejectsTrailingOperandsWithoutAnOperator()
    {
        var exception = Assert.Throws<PdfContentStreamException>(() => Parse("1 /Name"));

        Assert.Equal(7, exception.Offset);
        Assert.Contains("no operator", exception.Message);
    }

    [Fact]
    public void ParseTreatsRAsAnOperatorInsteadOfAnIndirectReferenceMarker()
    {
        var operations = Parse("1 0 R");

        var operation = Assert.Single(operations);
        AssertOperation(operation, "R", 2, 4);
        Assert.Equal(new PdfInteger(1), operation.Operands[0]);
        Assert.Equal(new PdfInteger(0), operation.Operands[1]);
    }

    [Fact]
    public void ParseRejectsAnIndirectReferenceInsideACompositeOperand()
    {
        var exception = Assert.Throws<PdfContentStreamException>(
            () => Parse("<< /Value 1 0 R >> op"));

        Assert.Equal(12, exception.Offset);
        Assert.Contains("Malformed content stream operand", exception.Message);
    }

    [Theory]
    [InlineData("[1 2 op", 5)]
    [InlineData("<< /A >> op", 6)]
    [InlineData("} op", 0)]
    public void ParseWrapsMalformedOperands(string content, int expectedOffset)
    {
        var exception = Assert.Throws<PdfContentStreamException>(() => Parse(content));

        Assert.Equal(expectedOffset, exception.Offset);
        Assert.IsType<PdfParseException>(exception.InnerException);
    }

    [Theory]
    [InlineData("BI", "BI")]
    [InlineData("ID", "ID")]
    [InlineData("EI", "EI")]
    public void ParseRejectsInlineImageSyntaxExplicitly(string content, string operatorName)
    {
        var exception = Assert.Throws<PdfContentStreamException>(() => Parse(content));

        Assert.Equal(operatorName, exception.Operator);
        Assert.Contains("Inline image", exception.Message);
    }

    [Fact]
    public void ParseRejectsNonAsciiOperators()
    {
        var exception = Assert.Throws<PdfContentStreamException>(
            () => new PdfContentStreamParser(new byte[] { 0x80 }).Parse());

        Assert.Equal(0, exception.Offset);
        Assert.Contains("ASCII", exception.Message);
    }

    [Fact]
    public void ConfiguredOperationLimitIsEnforced()
    {
        var options = new PdfContentStreamParserOptions { MaximumOperations = 1 };

        var exception = Assert.Throws<PdfContentStreamException>(() => Parse("q Q", options));

        Assert.Equal(2, exception.Offset);
        Assert.Contains("1 operations", exception.Message);
    }

    [Fact]
    public void ConfiguredOperandLimitIsEnforcedBeforeTheExcessOperand()
    {
        var options = new PdfContentStreamParserOptions { MaximumOperandsPerOperation = 1 };

        var exception = Assert.Throws<PdfContentStreamException>(() => Parse("1 2 m", options));

        Assert.Equal(2, exception.Offset);
        Assert.Contains("1 operands", exception.Message);
    }

    [Fact]
    public void ConfiguredOperatorAndObjectLimitsAreEnforced()
    {
        var operatorOptions = new PdfContentStreamParserOptions { MaximumOperatorLength = 1 };
        var operatorException = Assert.Throws<PdfContentStreamException>(
            () => Parse("LongOperator", operatorOptions));
        Assert.Equal(0, operatorException.Offset);

        var objectOptions = new PdfContentStreamParserOptions
        {
            ObjectParserOptions = new PdfObjectParserOptions { MaximumArrayItems = 1 },
        };
        var objectException = Assert.Throws<PdfContentStreamException>(
            () => Parse("[1 2] op", objectOptions));
        Assert.IsType<PdfParseException>(objectException.InnerException);
    }

    [Fact]
    public void InvalidOptionsAreRejectedAtConstruction()
    {
        var options = new PdfContentStreamParserOptions { MaximumOperations = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PdfContentStreamParser(ReadOnlyMemory<byte>.Empty, options));
    }

    [Fact]
    public void OperationOperandsAreSnapshotsAndReadOnly()
    {
        var operations = Parse("1 m 2 n");

        Assert.Equal(new PdfInteger(1), operations[0].Operands[0]);
        Assert.Equal(new PdfInteger(2), operations[1].Operands[0]);
        Assert.Throws<NotSupportedException>(
            () => ((IList<PdfObject>)operations[0].Operands).Add(new PdfInteger(3)));
    }

    private static IReadOnlyList<PdfContentOperation> Parse(
        string content,
        PdfContentStreamParserOptions? options = null) =>
        new PdfContentStreamParser(Encoding.Latin1.GetBytes(content), options).Parse();

    private static void AssertOperation(
        PdfContentOperation operation,
        string operatorName,
        int operandCount,
        int offset)
    {
        Assert.Equal(operatorName, operation.Operator);
        Assert.Equal(operandCount, operation.Operands.Count);
        Assert.Equal(offset, operation.Offset);
    }

    private static byte[] Compress(ReadOnlySpan<byte> input)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(input);
        }

        return output.ToArray();
    }
}
