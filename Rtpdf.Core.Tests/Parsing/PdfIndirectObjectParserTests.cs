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
    public void ParseRejectsStreamWithoutConsumingItAsADictionary()
    {
        const string input = "1 0 obj << /Length 0 >> stream\n\nendstream endobj";

        var exception = Assert.Throws<PdfParseException>(() =>
            new PdfIndirectObjectParser().Parse(Encoding.ASCII.GetBytes(input)));

        Assert.Contains("Stream objects", exception.Message);
    }
}
