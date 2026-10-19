using System.Text;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Parsing;
using Xunit;

namespace Rtpdf.Core.Tests.Parsing;

public sealed class PdfIndirectObjectParserTests
{
    [Fact]
    public void ParseReadsHeaderValueAndTerminator()
    {
        var input = Encoding.ASCII.GetBytes(
            "padding 12 3 obj % declaration\n<< /Type /Example /Items [1 2] >>\nendobj trailing");
        var offset = Encoding.ASCII.GetByteCount("padding ");

        var result = new PdfIndirectObjectParser().Parse(input, offset);

        Assert.Equal(new PdfObjectId(12, 3), result.ObjectId);
        var dictionary = Assert.IsType<PdfDictionary>(result.Value);
        Assert.Equal(new PdfName("Example"), dictionary["Type"]);
        Assert.Equal(2, Assert.IsType<PdfArray>(dictionary["Items"]).Count);
    }

    [Theory]
    [InlineData("0 0 obj null endobj")]
    [InlineData("1 65536 obj null endobj")]
    [InlineData("1 0 notobj null endobj")]
    [InlineData("1 0 obj null missing")]
    [InlineData("1 0 obj")]
    public void ParseRejectsMalformedIndirectObject(string input)
    {
        Assert.Throws<PdfParseException>(() =>
            new PdfIndirectObjectParser().Parse(Encoding.ASCII.GetBytes(input)));
    }

    [Fact]
    public void ParseReadsStreamByDeclaredLengthEvenWhenDataContainsEndstreamText()
    {
        var data = "abc\nendstream\nxyz"u8.ToArray();
        var prefix = Encoding.ASCII.GetBytes($"1 0 obj << /Length {data.Length} >> stream\n");
        var input = new byte[prefix.Length + data.Length + "\nendstream\nendobj"u8.Length];
        prefix.CopyTo(input, 0);
        data.CopyTo(input, prefix.Length);
        "\nendstream\nendobj"u8.CopyTo(input.AsSpan(prefix.Length + data.Length));

        var result = new PdfIndirectObjectParser().Parse(input);

        var stream = Assert.IsType<PdfStream>(result.Value);
        Assert.Equal(data, stream.GetEncodedDataCopy());
        Assert.Equal(new PdfInteger(data.Length), stream.Dictionary["Length"]);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\r\n")]
    public void ParseAcceptsPdfEndOfLineFormsAroundStreamData(string endOfLine)
    {
        var input = Encoding.ASCII.GetBytes(
            $"1 0 obj << /Length 3 >> stream{endOfLine}abc{endOfLine}endstream{endOfLine}endobj");

        var result = new PdfIndirectObjectParser().Parse(input);

        Assert.Equal("abc"u8.ToArray(), Assert.IsType<PdfStream>(result.Value).GetEncodedDataCopy());
    }

    [Fact]
    public void ParseAcceptsEmptyStreamWithEndstreamImmediatelyAfterOpeningLine()
    {
        const string input = "1 0 obj << /Length 0 >> stream\nendstream\nendobj";

        var result = new PdfIndirectObjectParser().Parse(Encoding.ASCII.GetBytes(input));

        Assert.Empty(Assert.IsType<PdfStream>(result.Value).EncodedData.ToArray());
    }

    [Theory]
    [InlineData("1 0 obj << >> stream\n\nendstream\nendobj")]
    [InlineData("1 0 obj << /Length -1 >> stream\n\nendstream\nendobj")]
    [InlineData("1 0 obj << /Length 5 >> stream\nabc\nendstream\nendobj")]
    [InlineData("1 0 obj << /Length 3 >> stream abc\nendstream\nendobj")]
    [InlineData("1 0 obj << /Length 3 >> stream\nabc\nnotendstream\nendobj")]
    [InlineData("1 0 obj << /Length 3 >> stream\nabc\nendstream\nnotendobj")]
    public void ParseRejectsMalformedStreamSyntax(string input)
    {
        Assert.Throws<PdfParseException>(() =>
            new PdfIndirectObjectParser().Parse(Encoding.ASCII.GetBytes(input)));
    }

    [Fact]
    public void ParseWithoutObjectStoreRejectsIndirectStreamLength()
    {
        const string input = "1 0 obj << /Length 2 0 R >> stream\nabc\nendstream\nendobj";

        var exception = Assert.Throws<PdfParseException>(() =>
            new PdfIndirectObjectParser().Parse(Encoding.ASCII.GetBytes(input)));

        Assert.Contains("object resolver", exception.Message);
    }

    [Fact]
    public void ParseEnforcesEncodedStreamLengthLimit()
    {
        const string input = "1 0 obj << /Length 3 >> stream\nabc\nendstream\nendobj";
        var options = new PdfObjectParserOptions { MaximumEncodedStreamLength = 2 };

        Assert.Throws<PdfParseException>(() =>
            new PdfIndirectObjectParser(options).Parse(Encoding.ASCII.GetBytes(input)));
    }
}
