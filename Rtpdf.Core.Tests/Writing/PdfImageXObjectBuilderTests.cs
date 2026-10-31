using System.IO.Compression;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Documents;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Pages;
using Rtpdf.Core.Writing;
using Rtpdf.Core.Writing.Content;
using Rtpdf.Core.Writing.Pages;
using Rtpdf.Core.Writing.Resources;
using Xunit;

namespace Rtpdf.Core.Tests.Writing;

public sealed class PdfImageXObjectBuilderTests
{
    [Theory]
    [InlineData(0xC0)]
    [InlineData(0xC2)]
    public void EmbedsRgbJpegDataWithoutRecompression(byte startOfFrameMarker)
    {
        var jpeg = CreateJpeg(320, 240, components: 3, startOfFrameMarker: startOfFrameMarker);

        var image = PdfImageXObjectBuilder.FromJpeg(jpeg);

        Assert.Equal(jpeg, image.EncodedData.ToArray());
        AssertImageDictionary(image.Dictionary, 320, 240, "DeviceRGB", "DCTDecode");
        Assert.Equal(new PdfInteger(8), image.Dictionary["BitsPerComponent"]);
    }

    [Fact]
    public void EmbedsAnEightBitGrayscaleJpeg()
    {
        var image = PdfImageXObjectBuilder.FromJpeg(CreateJpeg(12, 34, components: 1));

        AssertImageDictionary(image.Dictionary, 12, 34, "DeviceGray", "DCTDecode");
    }

    [Fact]
    public void CompressesRgb24RowsWithoutIncludingStridePadding()
    {
        byte[] pixels =
        [
            1, 2, 3, 4, 5, 6, 99, 99,
            7, 8, 9, 10, 11, 12, 88, 88,
        ];

        var image = PdfImageXObjectBuilder.FromRgb24(pixels, 2, 2, stride: 8);

        AssertImageDictionary(image.Dictionary, 2, 2, "DeviceRGB", "FlateDecode");
        Assert.Equal(
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 },
            Decompress(image.EncodedData.Span));
    }

    [Fact]
    public void ImagePageRoundTripsThroughWriterReaderAndStreamDecoder()
    {
        var document = new PdfDocumentBuilder();
        var page = new PdfPageTreeBuilder(document)
            .AddPage(new PdfRectangle(0, 0, 200, 100));
        var image = PdfImageXObjectBuilder.FromRgb24(
            [255, 0, 0, 0, 255, 0],
            width: 2,
            height: 1);
        var imageReference = page.AddXObject("Image1", image);
        page.AddContent(
            new PdfContentStreamBuilder()
                .SaveGraphicsState()
                .ConcatenateMatrix(200, 0, 0, 100, 0, 0)
                .DrawXObject("Image1")
                .RestoreGraphicsState()
                .Build());

        using var output = new MemoryStream();
        PdfDocumentWriter.Write(output, document, leaveOpen: true);
        using var parsed = PdfDocumentReader.Open(output.ToArray().AsMemory());
        var parsedPage = Assert.Single(parsed.Catalog.PageTree.Pages);
        var xObjects = Assert.IsType<PdfDictionary>(parsedPage.Resources!["XObject"]);
        Assert.Equal(imageReference, xObjects["Image1"]);
        var parsedImage = parsed.ObjectStore.Resolve<PdfStream>(imageReference);

        Assert.Equal(new byte[] { 255, 0, 0, 0, 255, 0 }, parsed.StreamDecoder.Decode(parsedImage));
    }

    [Fact]
    public void RejectsUnsupportedOrMalformedJpegData()
    {
        Assert.Throws<PdfWriteException>(() => PdfImageXObjectBuilder.FromJpeg([1, 2, 3]));
        Assert.Throws<PdfWriteException>(() => PdfImageXObjectBuilder.FromJpeg([0xFF, 0xD8, 0xFF, 0xD9]));
        Assert.Throws<PdfWriteException>(() =>
            PdfImageXObjectBuilder.FromJpeg(CreateJpeg(1, 1, components: 4)));
        Assert.Throws<PdfWriteException>(() =>
            PdfImageXObjectBuilder.FromJpeg(CreateJpeg(1, 1, components: 3, precision: 12)));
        Assert.Throws<PdfWriteException>(() =>
            PdfImageXObjectBuilder.FromJpeg(
                CreateJpeg(1, 1, components: 3, startOfFrameMarker: 0xC3)));

        var truncated = CreateJpeg(1, 1, components: 3)[..^5];
        Assert.Throws<PdfWriteException>(() => PdfImageXObjectBuilder.FromJpeg(truncated));
    }

    [Fact]
    public void RejectsInvalidRgbGeometryStrideAndLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PdfImageXObjectBuilder.FromRgb24([], 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PdfImageXObjectBuilder.FromRgb24(new byte[6], 2, 1, stride: 5));
        Assert.Throws<ArgumentException>(() =>
            PdfImageXObjectBuilder.FromRgb24(new byte[5], 2, 1));
    }

    [Fact]
    public void EnforcesImageSourcePixelAndEncodedLimits()
    {
        var jpeg = CreateJpeg(10, 10, components: 3);
        Assert.Throws<PdfWriteException>(() => PdfImageXObjectBuilder.FromJpeg(
            jpeg,
            new PdfImageXObjectOptions { MaximumPixels = 99 }));
        Assert.Throws<PdfWriteException>(() => PdfImageXObjectBuilder.FromJpeg(
            jpeg,
            new PdfImageXObjectOptions { MaximumSourceLength = jpeg.Length - 1 }));
        Assert.Throws<PdfWriteException>(() => PdfImageXObjectBuilder.FromJpeg(
            jpeg,
            new PdfImageXObjectOptions { MaximumEncodedLength = jpeg.Length - 1 }));

        Assert.Throws<PdfWriteException>(() => PdfImageXObjectBuilder.FromRgb24(
            new byte[12],
            2,
            2,
            options: new PdfImageXObjectOptions { MaximumPixels = 3 }));
    }

    private static byte[] CreateJpeg(
        ushort width,
        ushort height,
        byte components,
        byte precision = 8,
        byte startOfFrameMarker = 0xC0)
    {
        var segmentLength = 8 + (components * 3);
        var result = new List<byte>
        {
            0xFF, 0xD8,
            0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00,
            0xFF, startOfFrameMarker,
            (byte)(segmentLength >> 8), (byte)segmentLength,
            precision,
            (byte)(height >> 8), (byte)height,
            (byte)(width >> 8), (byte)width,
            components,
        };
        for (byte component = 1; component <= components; component++)
        {
            result.Add(component);
            result.Add(0x11);
            result.Add(0);
        }

        result.Add(0xFF);
        result.Add(0xD9);
        return result.ToArray();
    }

    private static byte[] Decompress(ReadOnlySpan<byte> encoded)
    {
        using var input = new MemoryStream(encoded.ToArray());
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static void AssertImageDictionary(
        PdfDictionary dictionary,
        int width,
        int height,
        string colorSpace,
        string filter)
    {
        Assert.Equal(new PdfName("XObject"), dictionary["Type"]);
        Assert.Equal(new PdfName("Image"), dictionary["Subtype"]);
        Assert.Equal(new PdfInteger(width), dictionary["Width"]);
        Assert.Equal(new PdfInteger(height), dictionary["Height"]);
        Assert.Equal(new PdfName(colorSpace), dictionary["ColorSpace"]);
        Assert.Equal(new PdfName(filter), dictionary["Filter"]);
    }
}
