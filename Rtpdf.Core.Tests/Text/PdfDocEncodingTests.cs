using Rtpdf.Core.Objects;
using Rtpdf.Core.Text;
using Xunit;

namespace Rtpdf.Core.Tests.Text;

public sealed class PdfDocEncodingTests
{
    [Fact]
    public void DecodeMapsAsciiControlsAndLatinOneRangesDirectly()
    {
        var bytes = Enumerable.Range(0x00, 0x18)
            .Concat(Enumerable.Range(0x20, 0x5F))
            .Concat(Enumerable.Range(0xA1, 0x5F))
            .Select(value => (byte)value)
            .ToArray();
        var expected = new string(bytes.Select(value => (char)value).ToArray());

        var decoded = PdfDocEncoding.Decode(bytes);

        Assert.Equal(expected, decoded);
    }

    [Fact]
    public void DecodeMapsPdfDocEncodingSpecialCharacters()
    {
        var lowSpecials = Enumerable.Range(0x18, 8).Select(value => (byte)value).ToArray();
        var highSpecials = Enumerable.Range(0x80, 0x1F).Select(value => (byte)value).ToArray();

        Assert.Equal("\u02D8\u02C7\u02C6\u02D9\u02DD\u02DB\u02DA\u02DC", PdfDocEncoding.Decode(lowSpecials));
        Assert.Equal(
            "\u2022\u2020\u2021\u2026\u2014\u2013\u0192\u2044" +
            "\u2039\u203A\u2212\u2030\u201E\u201C\u201D\u2018" +
            "\u2019\u201A\u2122\uFB01\uFB02\u0141\u0152\u0160" +
            "\u0178\u017D\u0131\u0142\u0153\u0161\u017E",
            PdfDocEncoding.Decode(highSpecials));
        Assert.Equal("\u20AC", PdfDocEncoding.Decode([0xA0]));
    }

    [Fact]
    public void DecodeReplacesUndefinedBytesAndTryDecodeRejectsThem()
    {
        Assert.Equal("A\uFFFDB\uFFFD", PdfDocEncoding.Decode([0x41, 0x7F, 0x42, 0x9F]));
        Assert.False(PdfDocEncoding.TryDecode([0x7F], out var invalid));
        Assert.Null(invalid);

        Assert.True(PdfDocEncoding.TryDecode([0x80, 0xA0], out var valid));
        Assert.Equal("\u2022\u20AC", valid);
    }

    [Fact]
    public void PdfStringUsesPdfDocEncodingWhenThereIsNoUnicodeBom()
    {
        var value = new PdfString([0x48, 0x80, 0xA0]);

        Assert.Equal("H\u2022\u20AC", value.GetText());
        Assert.Equal(new byte[] { 0x48, 0x80, 0xA0 }, value.Bytes.ToArray());
    }

    [Theory]
    [InlineData(new byte[] { 0xFE, 0xFF, 0x30, 0x42 })]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x42, 0x30 })]
    public void PdfStringUnicodeBomTakesPrecedenceOverPdfDocEncoding(byte[] bytes)
    {
        Assert.Equal("あ", new PdfString(bytes).GetText());
    }
}
