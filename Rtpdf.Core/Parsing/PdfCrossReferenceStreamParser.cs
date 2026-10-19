using Rtpdf.Core.CrossReferences;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Filters;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Tokens;

namespace Rtpdf.Core.Parsing;

internal sealed class PdfCrossReferenceStreamParser
{
    private const int MaximumFieldWidth = sizeof(ulong);

    private readonly byte[] _input;
    private readonly PdfCrossReferenceParserOptions _options;

    public PdfCrossReferenceStreamParser(byte[] input, PdfCrossReferenceParserOptions options)
    {
        _input = input;
        _options = options;
    }

    public PdfCrossReferenceSection ParseAt(long crossReferenceOffset, int startXrefKeywordOffset)
    {
        var offset = ClampOffset(crossReferenceOffset);
        PdfIndirectObject indirectObject;
        try
        {
            indirectObject = new PdfIndirectObjectParser(_options.ObjectParserOptions)
                .Parse(_input, offset);
        }
        catch (PdfParseException exception)
        {
            throw Error($"Invalid xref stream object: {exception.Message}", exception.Offset);
        }
        catch (PdfLexerException exception)
        {
            throw Error($"Invalid xref stream object: {exception.Message}", exception.Offset);
        }

        if (indirectObject.ObjectId.GenerationNumber != 0)
        {
            throw Error("An xref stream object must have generation number 0", offset);
        }

        if (indirectObject.Value is not PdfStream stream)
        {
            throw Error("Expected a conventional xref table or an xref stream object", offset);
        }

        var dictionary = stream.Dictionary;
        if (!dictionary.TryGetValue("Type", out var typeObject) ||
            typeObject is not PdfName { Value: "XRef" })
        {
            throw Error("Xref stream dictionary /Type must be /XRef", offset);
        }

        var declaredSize = ReadPositiveInt32(dictionary, "Size", offset);
        ValidatePreviousOffset(dictionary, crossReferenceOffset, offset);
        var widths = ReadWidths(dictionary, offset);
        var ranges = ReadIndexRanges(dictionary, declaredSize, offset);

        byte[] decoded;
        try
        {
            decoded = new PdfStreamDecoder(_options.StreamDecoderOptions).Decode(stream);
        }
        catch (PdfFilterException exception)
        {
            throw Error($"Could not decode xref stream: {exception.Message}", offset);
        }

        var entryWidth = widths[0] + widths[1] + widths[2];
        long entryCount = ranges.Sum(range => (long)range.Count);
        var expectedLength = entryCount * entryWidth;
        if (expectedLength != decoded.Length)
        {
            throw Error(
                $"Decoded xref stream length must be exactly {expectedLength} bytes but was {decoded.Length}",
                offset);
        }

        var entries = ReadEntries(decoded, widths, ranges, declaredSize, offset);
        return new PdfCrossReferenceSection(
            crossReferenceOffset,
            startXrefKeywordOffset,
            offset,
            declaredSize,
            entries,
            dictionary,
            PdfCrossReferenceSectionKind.Stream,
            indirectObject.ObjectId);
    }

    private SortedDictionary<int, PdfObjectLocation> ReadEntries(
        ReadOnlySpan<byte> data,
        IReadOnlyList<int> widths,
        IReadOnlyList<IndexRange> ranges,
        int declaredSize,
        int errorOffset)
    {
        var entries = new SortedDictionary<int, PdfObjectLocation>();
        var dataOffset = 0;

        foreach (var range in ranges)
        {
            for (var index = 0; index < range.Count; index++)
            {
                var objectNumber = range.FirstObjectNumber + index;
                var type = widths[0] == 0 ? 1UL : ReadField(data, ref dataOffset, widths[0]);
                var field2 = widths[1] == 0 ? 0UL : ReadField(data, ref dataOffset, widths[1]);
                var field3 = widths[2] == 0 ? 0UL : ReadField(data, ref dataOffset, widths[2]);

                var location = type switch
                {
                    0 => CreateFreeLocation(objectNumber, field2, field3, declaredSize, errorOffset),
                    1 => CreateInUseLocation(objectNumber, field2, field3, errorOffset),
                    2 => CreateCompressedLocation(objectNumber, field2, field3, declaredSize, errorOffset),
                    _ => null,
                };

                if (location is not null && !entries.TryAdd(objectNumber, location))
                {
                    throw Error($"Duplicate xref stream entry for object {objectNumber}", errorOffset);
                }
            }
        }

        return entries;
    }

    private PdfObjectLocation CreateFreeLocation(
        int objectNumber,
        ulong nextFreeObjectNumber,
        ulong generationNumber,
        int declaredSize,
        int errorOffset)
    {
        if (nextFreeObjectNumber >= (ulong)declaredSize)
        {
            throw Error("Free xref stream entry points beyond /Size", errorOffset);
        }

        return new PdfObjectLocation(
            objectNumber,
            (long)nextFreeObjectNumber,
            ReadGenerationNumber(generationNumber, errorOffset),
            PdfObjectLocationKind.Free);
    }

    private PdfObjectLocation CreateInUseLocation(
        int objectNumber,
        ulong byteOffset,
        ulong generationNumber,
        int errorOffset)
    {
        if (byteOffset >= (ulong)_input.Length)
        {
            throw Error("In-use xref stream entry points outside the input", errorOffset);
        }

        return new PdfObjectLocation(
            objectNumber,
            (long)byteOffset,
            ReadGenerationNumber(generationNumber, errorOffset),
            PdfObjectLocationKind.InUse);
    }

    private static PdfObjectLocation CreateCompressedLocation(
        int objectNumber,
        ulong objectStreamNumber,
        ulong objectStreamIndex,
        int declaredSize,
        int errorOffset)
    {
        if (objectStreamNumber is 0 || objectStreamNumber >= (ulong)declaredSize)
        {
            throw Error("Compressed xref entry has an invalid object stream number", errorOffset);
        }

        if (objectStreamIndex > int.MaxValue)
        {
            throw Error("Compressed xref entry index exceeds the supported 32-bit range", errorOffset);
        }

        return new PdfObjectLocation(
            objectNumber,
            (long)objectStreamNumber,
            generationNumber: 0,
            PdfObjectLocationKind.Compressed,
            (int)objectStreamIndex);
    }

    private static ushort ReadGenerationNumber(ulong value, int errorOffset)
    {
        if (value > ushort.MaxValue)
        {
            throw Error($"Xref generation number must be between 0 and {ushort.MaxValue}", errorOffset);
        }

        return (ushort)value;
    }

    private IReadOnlyList<int> ReadWidths(PdfDictionary dictionary, int errorOffset)
    {
        if (!dictionary.TryGetValue("W", out var widthObject) ||
            widthObject is not PdfArray { Count: 3 } widthArray)
        {
            throw Error("Xref stream /W must be an array of three integers", errorOffset);
        }

        var widths = new int[3];
        for (var index = 0; index < widths.Length; index++)
        {
            if (widthArray[index] is not PdfInteger width ||
                width.Value is < 0 or > MaximumFieldWidth)
            {
                throw Error(
                    $"Xref stream /W values must be integers between 0 and {MaximumFieldWidth}",
                    errorOffset);
            }

            widths[index] = (int)width.Value;
        }

        if (widths.Sum() == 0)
        {
            throw Error("Xref stream /W must define a non-zero entry width", errorOffset);
        }

        return widths;
    }

    private IReadOnlyList<IndexRange> ReadIndexRanges(
        PdfDictionary dictionary,
        int declaredSize,
        int errorOffset)
    {
        if (!dictionary.TryGetValue("Index", out var indexObject))
        {
            if (declaredSize > _options.MaximumEntries)
            {
                throw Error(
                    $"Xref stream exceeds the configured limit of {_options.MaximumEntries} entries",
                    errorOffset);
            }

            return [new IndexRange(0, declaredSize)];
        }

        if (indexObject is not PdfArray indexArray || indexArray.Count % 2 != 0)
        {
            throw Error("Xref stream /Index must be an array of integer pairs", errorOffset);
        }

        var ranges = new List<IndexRange>(indexArray.Count / 2);
        var previousRangeEnd = 0;
        var hasNonEmptyRange = false;
        long totalEntries = 0;
        for (var index = 0; index < indexArray.Count; index += 2)
        {
            var firstObjectNumber = ReadNonNegativeInt32(indexArray[index], "Index", errorOffset);
            var count = ReadNonNegativeInt32(indexArray[index + 1], "Index", errorOffset);
            if ((long)firstObjectNumber + count > declaredSize)
            {
                throw Error("Xref stream /Index range extends beyond /Size", errorOffset);
            }

            if (count > 0 && hasNonEmptyRange && firstObjectNumber < previousRangeEnd)
            {
                throw Error("Xref stream /Index ranges must not overlap or go backwards", errorOffset);
            }

            totalEntries += count;
            if (totalEntries > _options.MaximumEntries)
            {
                throw Error(
                    $"Xref stream exceeds the configured limit of {_options.MaximumEntries} entries",
                    errorOffset);
            }

            if (count > 0)
            {
                previousRangeEnd = firstObjectNumber + count;
                hasNonEmptyRange = true;
            }

            ranges.Add(new IndexRange(firstObjectNumber, count));
        }

        return ranges;
    }

    private static int ReadPositiveInt32(PdfDictionary dictionary, string key, int errorOffset)
    {
        if (!dictionary.TryGetValue(key, out var value) ||
            value is not PdfInteger integer ||
            integer.Value is <= 0 or > int.MaxValue)
        {
            throw Error($"Xref stream /{key} must be a positive 32-bit integer", errorOffset);
        }

        return (int)integer.Value;
    }

    private void ValidatePreviousOffset(
        PdfDictionary dictionary,
        long crossReferenceOffset,
        int errorOffset)
    {
        if (!dictionary.TryGetValue("Prev", out var previousObject))
        {
            return;
        }

        if (previousObject is not PdfInteger previousInteger ||
            previousInteger.Value < 0 || previousInteger.Value >= _input.Length)
        {
            throw Error("Xref stream /Prev must be a valid byte offset", errorOffset);
        }

        if (previousInteger.Value == crossReferenceOffset)
        {
            throw Error("Xref stream /Prev must not reference the current xref stream", errorOffset);
        }
    }

    private static int ReadNonNegativeInt32(PdfObject value, string key, int errorOffset)
    {
        if (value is not PdfInteger integer || integer.Value is < 0 or > int.MaxValue)
        {
            throw Error($"Xref stream /{key} values must be non-negative 32-bit integers", errorOffset);
        }

        return (int)integer.Value;
    }

    private static ulong ReadField(ReadOnlySpan<byte> data, ref int offset, int width)
    {
        ulong value = 0;
        for (var index = 0; index < width; index++)
        {
            value = (value << 8) | data[offset++];
        }

        return value;
    }

    private int ClampOffset(long offset) => (int)Math.Clamp(offset, 0, _input.Length - 1);

    private static PdfCrossReferenceException Error(string message, int offset) => new(message, offset);

    private readonly record struct IndexRange(int FirstObjectNumber, int Count);
}
