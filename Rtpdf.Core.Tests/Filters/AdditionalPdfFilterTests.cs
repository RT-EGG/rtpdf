using System.Text;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Filters;
using Rtpdf.Core.Objects;
using Xunit;

namespace Rtpdf.Core.Tests.Filters;

public sealed class AdditionalPdfFilterTests
{
    [Fact]
    public void AsciiHexDecodesWhitespaceAndPadsAnOddFinalNibble()
    {
        var decoded = Decode("ASCIIHexDecode", "61 62\r\n6>"u8.ToArray());

        Assert.Equal(new byte[] { 0x61, 0x62, 0x60 }, decoded);
    }

    [Fact]
    public void AsciiHexAllowsTheStreamBoundaryAsEndOfData()
    {
        Assert.Equal("abc"u8.ToArray(), Decode("ASCIIHexDecode", "616263"u8.ToArray()));
    }

    [Theory]
    [InlineData("6G>")]
    [InlineData("61>x")]
    public void AsciiHexRejectsMalformedData(string encoded)
    {
        var exception = Assert.Throws<PdfFilterException>(
            () => Decode("ASCIIHexDecode", Encoding.ASCII.GetBytes(encoded)));

        Assert.Equal("ASCIIHexDecode", exception.FilterName);
    }

    [Fact]
    public void Ascii85DecodesFullPartialAndZeroGroups()
    {
        var hello = Decode("ASCII85Decode", "87cURD_*#TDfTZ)+T~>"u8.ToArray());
        var zeroAndPartial = Decode("ASCII85Decode", "z!!*-'~>"u8.ToArray());

        Assert.Equal("Hello, world!"u8.ToArray(), hello);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 1, 2, 3 }, zeroAndPartial);
    }

    [Theory]
    [InlineData("!~>")]
    [InlineData("!z!!!~>")]
    [InlineData("uuuuu~>")]
    [InlineData("!!!!!")]
    [InlineData("!!!!!~ >")]
    [InlineData("!!!!!~>x")]
    public void Ascii85RejectsMalformedData(string encoded)
    {
        Assert.Throws<PdfFilterException>(
            () => Decode("ASCII85Decode", Encoding.ASCII.GetBytes(encoded)));
    }

    [Fact]
    public void RunLengthDecodesLiteralAndRepeatedRuns()
    {
        var encoded = new byte[] { 2, (byte)'A', (byte)'B', (byte)'C', 254, (byte)'Z', 128 };

        var decoded = Decode("RunLengthDecode", encoded);

        Assert.Equal("ABCZZZ"u8.ToArray(), decoded);
    }

    [Theory]
    [MemberData(nameof(InvalidRunLengthData))]
    public void RunLengthRejectsMalformedData(byte[] encoded)
    {
        Assert.Throws<PdfFilterException>(() => Decode("RunLengthDecode", encoded));
    }

    [Fact]
    public void LzwDecodesLiteralCodesAndTheSpecialNextDictionaryCode()
    {
        var literals = PackLzwCodes([256, (byte)'A', (byte)'B', (byte)'C', 257], earlyChange: 1);
        var repeated = PackLzwCodes([256, (byte)'A', 258, 257], earlyChange: 1);

        Assert.Equal("ABC"u8.ToArray(), Decode("LZWDecode", literals));
        Assert.Equal("AAA"u8.ToArray(), Decode("LZWDecode", repeated));
    }

    [Fact]
    public void LzwDecodesTheIso32000ExampleBitstream()
    {
        var encoded = new byte[] { 0x80, 0x0B, 0x60, 0x50, 0x22, 0x0C, 0x0C, 0x85, 0x01 };

        var decoded = Decode("LZWDecode", encoded);

        Assert.Equal("-----A---B"u8.ToArray(), decoded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void LzwSupportsBothEarlyChangeModesAcrossCodeWidthGrowth(int earlyChange)
    {
        var literalCodes = Enumerable.Repeat((int)(byte)'A', 300).ToArray();
        var codes = new[] { 256 }.Concat(literalCodes).Append(257).ToArray();
        var parameters = new PdfDictionary { ["EarlyChange"] = new PdfInteger(earlyChange) };

        var decoded = Decode("LZWDecode", PackLzwCodes(codes, earlyChange), parameters);

        Assert.Equal(Enumerable.Repeat((byte)'A', 300), decoded);
    }

    [Fact]
    public void LzwAppliesTheSharedPredictorAfterDecompression()
    {
        var predicted = new byte[] { 0, 10, 20, 30 };
        var codes = new[] { 256 }.Concat(predicted.Select(value => (int)value)).Append(257).ToArray();
        var parameters = new PdfDictionary
        {
            ["Predictor"] = new PdfInteger(15),
            ["Colors"] = new PdfInteger(1),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["Columns"] = new PdfInteger(3),
        };

        var decoded = Decode("LZWDecode", PackLzwCodes(codes, 1), parameters);

        Assert.Equal(new byte[] { 10, 20, 30 }, decoded);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void LzwRejectsInvalidEarlyChange(int earlyChange)
    {
        var parameters = new PdfDictionary { ["EarlyChange"] = new PdfInteger(earlyChange) };

        Assert.Throws<PdfFilterException>(() =>
            Decode("LZWDecode", PackLzwCodes([256, 257], 1), parameters));
    }

    [Fact]
    public void LzwRejectsUndefinedCodesAndMissingEndOfData()
    {
        Assert.Throws<PdfFilterException>(() =>
            Decode("LZWDecode", PackLzwCodes([256, 300, 257], 1)));
        Assert.Throws<PdfFilterException>(() =>
            Decode("LZWDecode", PackLzwCodes([256, 65], 1)));
        Assert.Throws<PdfFilterException>(() =>
            Decode("LZWDecode", PackLzwCodes([65, 257], 1)));
    }

    [Fact]
    public void DecoderAppliesNewFiltersInAChain()
    {
        var runLengthEncoded = new byte[] { 2, (byte)'A', (byte)'B', (byte)'C', 128 };
        var asciiHexEncoded = Encoding.ASCII.GetBytes(Convert.ToHexString(runLengthEncoded) + ">");
        var dictionary = new PdfDictionary
        {
            ["Filter"] = new PdfArray(
            [
                new PdfName("ASCIIHexDecode"),
                new PdfName("RunLengthDecode"),
            ]),
        };

        var decoded = new PdfStreamDecoder().Decode(new PdfStream(dictionary, asciiHexEncoded));

        Assert.Equal("ABC"u8.ToArray(), decoded);
    }

    [Theory]
    [InlineData("ASCIIHexDecode", "4142>")]
    [InlineData("ASCII85Decode", "5sbPO~>")]
    public void TextFiltersEnforceTheDecodedLengthLimit(string filterName, string encoded)
    {
        var exception = Assert.Throws<PdfFilterException>(() =>
            Decode(filterName, Encoding.ASCII.GetBytes(encoded), maximumDecodedLength: 1));

        Assert.Contains("limit", exception.Message);
    }

    [Fact]
    public void RunLengthAndLzwEnforceTheDecodedLengthLimit()
    {
        Assert.Throws<PdfFilterException>(() =>
            Decode("RunLengthDecode", [254, (byte)'A', 128], maximumDecodedLength: 2));
        Assert.Throws<PdfFilterException>(() =>
            Decode(
                "LZWDecode",
                PackLzwCodes([256, 65, 66, 257], 1),
                maximumDecodedLength: 1));
    }

    public static TheoryData<byte[]> InvalidRunLengthData => new()
    {
        Array.Empty<byte>(),
        new byte[] { 2, (byte)'A', 128 },
        new byte[] { 255 },
        new byte[] { 0, (byte)'A' },
    };

    private static byte[] Decode(
        string filterName,
        byte[] encoded,
        PdfDictionary? parameters = null,
        int maximumDecodedLength = 1024)
    {
        var dictionary = new PdfDictionary { ["Filter"] = new PdfName(filterName) };
        if (parameters is not null)
        {
            dictionary["DecodeParms"] = parameters;
        }

        return new PdfStreamDecoder(new PdfStreamDecoderOptions
        {
            MaximumDecodedStreamLength = maximumDecodedLength,
        }).Decode(new PdfStream(dictionary, encoded));
    }

    private static byte[] PackLzwCodes(IReadOnlyList<int> codes, int earlyChange)
    {
        var bytes = new List<byte>();
        var currentByte = 0;
        var bitsInCurrentByte = 0;
        var codeSize = 9;
        var nextCode = 258;
        var hasPreviousCode = false;

        foreach (var code in codes)
        {
            for (var bit = codeSize - 1; bit >= 0; bit--)
            {
                currentByte = (currentByte << 1) | ((code >> bit) & 1);
                bitsInCurrentByte++;
                if (bitsInCurrentByte == 8)
                {
                    bytes.Add((byte)currentByte);
                    currentByte = 0;
                    bitsInCurrentByte = 0;
                }
            }

            if (code == 256)
            {
                codeSize = 9;
                nextCode = 258;
                hasPreviousCode = false;
            }
            else if (code != 257)
            {
                if (hasPreviousCode && nextCode <= 4095)
                {
                    nextCode++;
                    if (codeSize < 12 && nextCode == (1 << codeSize) - earlyChange)
                    {
                        codeSize++;
                    }
                }

                hasPreviousCode = true;
            }
        }

        if (bitsInCurrentByte != 0)
        {
            bytes.Add((byte)(currentByte << (8 - bitsInCurrentByte)));
        }

        return bytes.ToArray();
    }
}
