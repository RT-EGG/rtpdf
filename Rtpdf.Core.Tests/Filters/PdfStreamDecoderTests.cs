using System.IO.Compression;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Filters;
using Rtpdf.Core.Objects;
using Xunit;

namespace Rtpdf.Core.Tests.Filters;

public sealed class PdfStreamDecoderTests
{
    [Fact]
    public void DecodeAppliesFlateWithoutMutatingEncodedData()
    {
        var original = "A content stream with repeated repeated repeated text."u8.ToArray();
        var encoded = Compress(original);
        var dictionary = new PdfDictionary
        {
            ["Length"] = new PdfInteger(encoded.Length),
            ["Filter"] = new PdfName("FlateDecode"),
        };
        var stream = new PdfStream(dictionary, encoded);

        var decoded = new PdfStreamDecoder().Decode(stream);

        Assert.Equal(original, decoded);
        Assert.Equal(encoded, stream.GetEncodedDataCopy());
    }

    [Fact]
    public void DecodeAppliesFilterArrayInDeclaredOrder()
    {
        var original = "two compression stages"u8.ToArray();
        var encoded = Compress(Compress(original));
        var dictionary = new PdfDictionary
        {
            ["Filter"] = new PdfArray(
            [
                new PdfName("FlateDecode"),
                new PdfName("FlateDecode"),
            ]),
            ["DecodeParms"] = new PdfArray([PdfNull.Instance, new PdfDictionary()]),
        };

        var decoded = new PdfStreamDecoder().Decode(new PdfStream(dictionary, encoded));

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void DecodeWithoutFilterReturnsAnIndependentCopy()
    {
        var stream = new PdfStream(new PdfDictionary(), [1, 2, 3]);

        var decoded = new PdfStreamDecoder().Decode(stream);
        decoded[0] = 9;

        Assert.Equal(new byte[] { 1, 2, 3 }, stream.GetEncodedDataCopy());
    }

    [Fact]
    public void DecodeRejectsUnsupportedAndInvalidFlateData()
    {
        var unsupported = new PdfStream(
            new PdfDictionary { ["Filter"] = new PdfName("LZWDecode") },
            [1, 2, 3]);
        var invalidFlate = new PdfStream(
            new PdfDictionary { ["Filter"] = new PdfName("FlateDecode") },
            [1, 2, 3]);

        var unsupportedError = Assert.Throws<PdfFilterException>(() =>
            new PdfStreamDecoder().Decode(unsupported));
        var invalidError = Assert.Throws<PdfFilterException>(() =>
            new PdfStreamDecoder().Decode(invalidFlate));

        Assert.Equal("LZWDecode", unsupportedError.FilterName);
        Assert.Equal("FlateDecode", invalidError.FilterName);
        Assert.IsType<InvalidDataException>(invalidError.InnerException);
    }

    [Fact]
    public void DecodeEnforcesDecodedLengthLimitDuringInflation()
    {
        var original = Enumerable.Repeat((byte)'A', 10_000).ToArray();
        var encoded = Compress(original);
        var stream = new PdfStream(
            new PdfDictionary { ["Filter"] = new PdfName("FlateDecode") },
            encoded);
        var options = new PdfStreamDecoderOptions { MaximumDecodedStreamLength = 100 };

        var exception = Assert.Throws<PdfFilterException>(() =>
            new PdfStreamDecoder(options).Decode(stream));

        Assert.Contains("limit", exception.Message);
    }

    [Fact]
    public void DecodeValidatesFilterAndDecodeParameterShapes()
    {
        var malformedFilters = new PdfStream(
            new PdfDictionary { ["Filter"] = new PdfArray([new PdfInteger(1)]) },
            []);
        var mismatchedParameters = new PdfStream(
            new PdfDictionary
            {
                ["Filter"] = new PdfArray([new PdfName("FlateDecode")]),
                ["DecodeParms"] = new PdfDictionary(),
            },
            []);

        Assert.Throws<PdfFilterException>(() => new PdfStreamDecoder().Decode(malformedFilters));
        Assert.Throws<PdfFilterException>(() => new PdfStreamDecoder().Decode(mismatchedParameters));
    }

    [Fact]
    public void FlateDecodeAcceptsDefaultPredictorAndRejectsUnsupportedPredictors()
    {
        var encoded = Compress("data"u8.ToArray());
        var defaultPredictor = new PdfStream(
            new PdfDictionary
            {
                ["Filter"] = new PdfName("FlateDecode"),
                ["DecodeParms"] = new PdfDictionary { ["Predictor"] = new PdfInteger(1) },
            },
            encoded);
        var pngPredictor = new PdfStream(
            new PdfDictionary
            {
                ["Filter"] = new PdfName("FlateDecode"),
                ["DecodeParms"] = new PdfDictionary { ["Predictor"] = new PdfInteger(12) },
            },
            encoded);

        Assert.Equal("data"u8.ToArray(), new PdfStreamDecoder().Decode(defaultPredictor));
        Assert.Throws<PdfFilterException>(() => new PdfStreamDecoder().Decode(pngPredictor));
    }

    [Fact]
    public void DecoderOptionsRejectInvalidLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfStreamDecoder(new PdfStreamDecoderOptions { MaximumFilterCount = -1 }));
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
}
