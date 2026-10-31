using System.Buffers.Binary;
using System.IO.Compression;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Writing.Resources;

/// <summary>Creates image XObject streams from encoded JPEG data or decoded pixels.</summary>
public static class PdfImageXObjectBuilder
{
    /// <summary>Embeds an 8-bit grayscale or RGB JPEG without recompressing it.</summary>
    public static PdfStream FromJpeg(
        ReadOnlySpan<byte> jpegData,
        PdfImageXObjectOptions? options = null)
    {
        var validatedOptions = ValidateOptions(options);
        ValidateSourceLength(jpegData.Length, validatedOptions);
        ValidateEncodedLength(jpegData.Length, validatedOptions);
        var information = ReadJpegInformation(jpegData);
        ValidatePixelCount(information.Width, information.Height, validatedOptions);
        var colorSpace = information.Components switch
        {
            1 => "DeviceGray",
            3 => "DeviceRGB",
            _ => throw new PdfWriteException(
                $"JPEG images with {information.Components} color components are not supported"),
        };

        return new PdfStream(
            CreateImageDictionary(
                information.Width,
                information.Height,
                information.Precision,
                colorSpace,
                "DCTDecode"),
            jpegData);
    }

    /// <summary>
    /// Creates an 8-bit DeviceRGB image from row-major RGB pixels and compresses it with FlateDecode.
    /// A stride may include padding bytes at the end of each input row.
    /// </summary>
    public static PdfStream FromRgb24(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int? stride = null,
        PdfImageXObjectOptions? options = null)
    {
        var validatedOptions = ValidateOptions(options);
        ValidateDimensions(width, height);
        ValidatePixelCount(width, height, validatedOptions);
        ValidateSourceLength(pixels.Length, validatedOptions);

        var rowLengthValue = (long)width * 3;
        if (rowLengthValue > int.MaxValue)
        {
            throw new PdfWriteException("RGB24 row length exceeds the supported in-memory range");
        }

        var rowLength = (int)rowLengthValue;
        var actualStride = stride ?? rowLength;
        if (actualStride < rowLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stride),
                "RGB24 stride must be at least width multiplied by 3");
        }

        var expectedLength = (long)actualStride * height;
        if (pixels.Length != expectedLength)
        {
            throw new ArgumentException(
                $"RGB24 source length must be exactly {expectedLength} bytes for the supplied dimensions and stride",
                nameof(pixels));
        }

        using var encoded = new MemoryStream();
        using (var compressor = new ZLibStream(
            encoded,
            validatedOptions.CompressionLevel,
            leaveOpen: true))
        {
            for (var row = 0; row < height; row++)
            {
                compressor.Write(pixels.Slice(row * actualStride, rowLength));
            }
        }

        ValidateEncodedLength(encoded.Length, validatedOptions);

        return new PdfStream(
            CreateImageDictionary(width, height, 8, "DeviceRGB", "FlateDecode"),
            encoded.ToArray());
    }

    private static PdfDictionary CreateImageDictionary(
        int width,
        int height,
        int bitsPerComponent,
        string colorSpace,
        string filter) =>
        new()
        {
            ["Type"] = new PdfName("XObject"),
            ["Subtype"] = new PdfName("Image"),
            ["Width"] = new PdfInteger(width),
            ["Height"] = new PdfInteger(height),
            ["ColorSpace"] = new PdfName(colorSpace),
            ["BitsPerComponent"] = new PdfInteger(bitsPerComponent),
            ["Filter"] = new PdfName(filter),
        };

    private static JpegInformation ReadJpegInformation(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            throw new PdfWriteException("JPEG data does not begin with an SOI marker");
        }

        var offset = 2;
        while (offset < data.Length)
        {
            if (data[offset] != 0xFF)
            {
                throw new PdfWriteException($"Invalid JPEG marker at byte offset {offset}");
            }

            while (offset < data.Length && data[offset] == 0xFF)
            {
                offset++;
            }

            if (offset >= data.Length)
            {
                break;
            }

            var marker = data[offset++];
            if (marker is 0xD9 or 0xDA)
            {
                break;
            }

            if (marker is 0x01 or >= 0xD0 and <= 0xD8)
            {
                continue;
            }

            if (offset > data.Length - 2)
            {
                throw new PdfWriteException("JPEG marker length is truncated");
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
            if (segmentLength < 2 || segmentLength > data.Length - offset)
            {
                throw new PdfWriteException("JPEG marker segment extends beyond the source data");
            }

            if (IsStartOfFrame(marker))
            {
                if (segmentLength < 8)
                {
                    throw new PdfWriteException("JPEG start-of-frame segment is too short");
                }

                var precision = data[offset + 2];
                var height = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 3)..]);
                var width = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 5)..]);
                var components = data[offset + 7];
                if (segmentLength < 8 + (components * 3))
                {
                    throw new PdfWriteException("JPEG start-of-frame component data is truncated");
                }

                if (precision != 8)
                {
                    throw new PdfWriteException(
                        $"JPEG sample precision {precision} is not supported; only 8-bit JPEG is supported");
                }

                ValidateDimensions(width, height);
                return new JpegInformation(width, height, precision, components);
            }

            offset += segmentLength;
        }

        throw new PdfWriteException("JPEG data does not contain a supported start-of-frame marker");
    }

    private static bool IsStartOfFrame(byte marker) => marker is 0xC0 or 0xC1 or 0xC2;

    private static PdfImageXObjectOptions ValidateOptions(PdfImageXObjectOptions? options)
    {
        var result = options ?? new PdfImageXObjectOptions();
        result.Validate();
        return result;
    }

    private static void ValidateDimensions(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(height, 0);
    }

    private static void ValidatePixelCount(
        int width,
        int height,
        PdfImageXObjectOptions options)
    {
        var pixels = (long)width * height;
        if (pixels > options.MaximumPixels)
        {
            throw new PdfWriteException(
                $"Image contains {pixels} pixels, exceeding the configured limit of {options.MaximumPixels}");
        }
    }

    private static void ValidateSourceLength(int length, PdfImageXObjectOptions options)
    {
        if (length > options.MaximumSourceLength)
        {
            throw new PdfWriteException(
                $"Image source exceeds the configured limit of {options.MaximumSourceLength} bytes");
        }
    }

    private static void ValidateEncodedLength(long length, PdfImageXObjectOptions options)
    {
        if (length > options.MaximumEncodedLength)
        {
            throw new PdfWriteException(
                $"Encoded image exceeds the configured limit of {options.MaximumEncodedLength} bytes");
        }
    }

    private readonly record struct JpegInformation(
        int Width,
        int Height,
        int Precision,
        int Components);
}
