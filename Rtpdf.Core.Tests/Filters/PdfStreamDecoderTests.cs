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
        var unsupportedPredictor = new PdfStream(
            new PdfDictionary
            {
                ["Filter"] = new PdfName("FlateDecode"),
                ["DecodeParms"] = new PdfDictionary { ["Predictor"] = new PdfInteger(3) },
            },
            encoded);

        Assert.Equal("data"u8.ToArray(), new PdfStreamDecoder().Decode(defaultPredictor));
        Assert.Throws<PdfFilterException>(() => new PdfStreamDecoder().Decode(unsupportedPredictor));
    }

    [Fact]
    public void FlateDecodeAppliesTiffPredictorForEightBitComponents()
    {
        var predicted = new byte[]
        {
            10, 20, 30, 5, 5, 5,
            100, 110, 120, 10, 20, 30,
        };
        var expected = new byte[]
        {
            10, 20, 30, 15, 25, 35,
            100, 110, 120, 110, 130, 150,
        };

        var decoded = DecodeWithPredictor(predicted, predictor: 2, colors: 3, bits: 8, columns: 2);

        Assert.Equal(expected, decoded);
    }

    [Theory]
    [InlineData(1, 8, 0x5E, 0x6B)]
    [InlineData(2, 4, 0x55, 0x6C)]
    [InlineData(4, 2, 0x25, 0x27)]
    public void FlateDecodeAppliesTiffPredictorForPackedComponents(
        int bits,
        int columns,
        byte predicted,
        byte expected)
    {
        var decoded = DecodeWithPredictor([predicted], 2, colors: 1, bits, columns);

        Assert.Equal(new[] { expected }, decoded);
    }

    [Fact]
    public void FlateDecodeAppliesTiffPredictorForSixteenBitComponents()
    {
        var predicted = new byte[] { 0x01, 0x00, 0x00, 0x02, 0x00, 0xFE };

        var decoded = DecodeWithPredictor(predicted, 2, colors: 1, bits: 16, columns: 3);

        Assert.Equal(new byte[] { 0x01, 0x00, 0x01, 0x02, 0x02, 0x00 }, decoded);
    }

    [Theory]
    [InlineData(10, 0, 10, 20, 35, 35)]
    [InlineData(11, 1, 10, 10, 15, 0)]
    [InlineData(12, 2, 10, 20, 35, 35)]
    [InlineData(13, 3, 10, 15, 25, 18)]
    [InlineData(14, 4, 10, 10, 15, 0)]
    public void FlateDecodeAppliesFixedPngPredictors(
        int predictor,
        byte rowFilter,
        byte first,
        byte second,
        byte third,
        byte fourth)
    {
        var predicted = new[] { rowFilter, first, second, third, fourth };

        var decoded = DecodeWithPredictor(predicted, predictor, colors: 1, bits: 8, columns: 4);

        Assert.Equal(new byte[] { 10, 20, 35, 35 }, decoded);
    }

    [Fact]
    public void FlateDecodeAppliesPngOptimumPredictorPerRow()
    {
        var predicted = new byte[]
        {
            0, 10, 20, 30, 40, 50, 60,
            1, 15, 50, 10, 65, 246, 90,
            2, 5, 30, 236, 40, 246, 40,
            3, 10, 40, 123, 50, 231, 203,
            4, 251, 226, 20, 216, 10, 90,
        };
        var expected = new byte[]
        {
            10, 20, 30, 40, 50, 60,
            15, 50, 10, 80, 40, 100,
            20, 80, 246, 120, 30, 140,
            20, 80, 246, 120, 30, 140,
            15, 50, 10, 80, 40, 100,
        };

        var decoded = DecodeWithPredictor(predicted, 15, colors: 3, bits: 8, columns: 2);

        Assert.Equal(expected, decoded);
    }

    [Fact]
    public void FlateDecodeUsesRoundedByteWidthForPackedPngPixels()
    {
        var predicted = new byte[] { 1, 0x6B, 0x14 };

        var decoded = DecodeWithPredictor(predicted, 11, colors: 1, bits: 1, columns: 16);

        Assert.Equal(new byte[] { 0x6B, 0x7F }, decoded);
    }

    [Fact]
    public void PngPredictorLimitAppliesToOutputRatherThanRowPrefixes()
    {
        var predicted = new byte[] { 0, 1, 2, 3, 0, 4, 5, 6 };
        var stream = CreatePredictorStream(predicted, 15, colors: 1, bits: 8, columns: 3);

        var decoded = new PdfStreamDecoder(new PdfStreamDecoderOptions
        {
            MaximumDecodedStreamLength = 6,
        }).Decode(stream);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, decoded);
        Assert.Throws<PdfFilterException>(() =>
            new PdfStreamDecoder(new PdfStreamDecoderOptions
            {
                MaximumDecodedStreamLength = 5,
            }).Decode(stream));
    }

    [Theory]
    [MemberData(nameof(InvalidPredictorParameters))]
    public void FlateDecodeRejectsInvalidPredictorParameters(PdfDictionary parameters)
    {
        var stream = new PdfStream(
            new PdfDictionary
            {
                ["Filter"] = new PdfName("FlateDecode"),
                ["DecodeParms"] = parameters,
            },
            Compress([]));

        Assert.Throws<PdfFilterException>(() => new PdfStreamDecoder().Decode(stream));
    }

    [Fact]
    public void FlateDecodeRejectsIncompleteRowsAndInvalidPngFilters()
    {
        var incomplete = CreatePredictorStream([0, 1, 2], 15, 1, 8, 3);
        var invalidFilter = CreatePredictorStream([5, 1, 2, 3], 15, 1, 8, 3);
        var mismatchedFixedFilter = CreatePredictorStream([0, 1, 2, 3], 11, 1, 8, 3);

        Assert.Throws<PdfFilterException>(() => new PdfStreamDecoder().Decode(incomplete));
        Assert.Throws<PdfFilterException>(() => new PdfStreamDecoder().Decode(invalidFilter));
        Assert.Throws<PdfFilterException>(() => new PdfStreamDecoder().Decode(mismatchedFixedFilter));
    }

    [Fact]
    public void DecoderOptionsRejectInvalidLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfStreamDecoder(new PdfStreamDecoderOptions { MaximumFilterCount = -1 }));
    }

    public static TheoryData<PdfDictionary> InvalidPredictorParameters => new()
    {
        new PdfDictionary { ["Predictor"] = new PdfName("bad") },
        new PdfDictionary { ["Predictor"] = new PdfInteger(3) },
        new PdfDictionary
        {
            ["Predictor"] = new PdfInteger(2),
            ["Colors"] = new PdfInteger(0),
        },
        new PdfDictionary
        {
            ["Predictor"] = new PdfInteger(2),
            ["BitsPerComponent"] = new PdfInteger(3),
        },
        new PdfDictionary
        {
            ["Predictor"] = new PdfInteger(15),
            ["Columns"] = new PdfInteger(0),
        },
        new PdfDictionary
        {
            ["Predictor"] = new PdfInteger(15),
            ["Colors"] = new PdfInteger(int.MaxValue),
            ["Columns"] = new PdfInteger(int.MaxValue),
        },
    };

    private static byte[] DecodeWithPredictor(
        byte[] predicted,
        int predictor,
        int colors,
        int bits,
        int columns) =>
        new PdfStreamDecoder().Decode(
            CreatePredictorStream(predicted, predictor, colors, bits, columns));

    private static PdfStream CreatePredictorStream(
        byte[] predicted,
        int predictor,
        int colors,
        int bits,
        int columns)
    {
        var encoded = Compress(predicted);
        return new PdfStream(
            new PdfDictionary
            {
                ["Length"] = new PdfInteger(encoded.Length),
                ["Filter"] = new PdfName("FlateDecode"),
                ["DecodeParms"] = new PdfDictionary
                {
                    ["Predictor"] = new PdfInteger(predictor),
                    ["Colors"] = new PdfInteger(colors),
                    ["BitsPerComponent"] = new PdfInteger(bits),
                    ["Columns"] = new PdfInteger(columns),
                },
            },
            encoded);
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
