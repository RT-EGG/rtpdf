using System.Globalization;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Parsing;
using Rtpdf.Core.Tokens;

namespace Rtpdf.Core.Storage;

internal sealed class PdfObjectStreamData
{
    private readonly byte[] _decodedData;
    private readonly int _firstObjectOffset;
    private readonly Entry[] _entries;

    private PdfObjectStreamData(byte[] decodedData, int firstObjectOffset, Entry[] entries)
    {
        _decodedData = decodedData;
        _firstObjectOffset = firstObjectOffset;
        _entries = entries;
    }

    public int DecodedLength => _decodedData.Length;

    public static PdfObjectStreamData Parse(
        byte[] decodedData,
        int objectCount,
        int firstObjectOffset,
        int maximumObjects)
    {
        ArgumentNullException.ThrowIfNull(decodedData);
        if (objectCount < 0 || objectCount > maximumObjects)
        {
            throw new InvalidDataException(
                $"Object stream /N must be between 0 and {maximumObjects}");
        }

        if (firstObjectOffset < 0 || firstObjectOffset > decodedData.Length)
        {
            throw new InvalidDataException(
                "Object stream /First must be within the decoded stream");
        }

        var header = decodedData.AsMemory(0, firstObjectOffset);
        var lexer = new PdfLexer(header);
        var entries = new Entry[objectCount];
        var objectNumbers = new HashSet<int>();
        var previousOffset = -1;

        for (var index = 0; index < objectCount; index++)
        {
            var objectNumber = ReadHeaderInteger(lexer, "object number");
            var relativeOffset = ReadHeaderInteger(lexer, "object offset");
            if (objectNumber <= 0)
            {
                throw new InvalidDataException("Object stream object numbers must be positive");
            }

            if (!objectNumbers.Add(objectNumber))
            {
                throw new InvalidDataException(
                    $"Object stream contains duplicate object number {objectNumber}");
            }

            if (relativeOffset < 0 || relativeOffset <= previousOffset)
            {
                throw new InvalidDataException(
                    "Object stream object offsets must be non-negative and strictly increasing");
            }

            if (index == 0 && relativeOffset != 0)
            {
                throw new InvalidDataException(
                    "The first object stream object must have relative offset 0");
            }

            var absoluteOffset = (long)firstObjectOffset + relativeOffset;
            if (absoluteOffset >= decodedData.Length)
            {
                throw new InvalidDataException("Object stream object offset is outside the decoded stream");
            }

            entries[index] = new Entry(objectNumber, relativeOffset);
            previousOffset = relativeOffset;
        }

        if (lexer.Read().Kind != PdfTokenKind.EndOfInput)
        {
            throw new InvalidDataException("Object stream header contains unexpected data before /First");
        }

        return new PdfObjectStreamData(decodedData, firstObjectOffset, entries);
    }

    public PdfIndirectObject ReadObject(
        PdfObjectId requestedObjectId,
        int objectIndex,
        PdfObjectParserOptions parserOptions)
    {
        if (requestedObjectId.GenerationNumber != 0)
        {
            throw new InvalidDataException("Compressed objects must have generation number 0");
        }

        if (objectIndex < 0 || objectIndex >= _entries.Length)
        {
            throw new InvalidDataException("Compressed-object index is outside object stream /N");
        }

        var entry = _entries[objectIndex];
        if (entry.ObjectNumber != requestedObjectId.ObjectNumber)
        {
            throw new InvalidDataException(
                $"Object stream index {objectIndex} identifies object {entry.ObjectNumber}, not {requestedObjectId.ObjectNumber}");
        }

        var start = _firstObjectOffset + entry.RelativeOffset;
        var end = objectIndex + 1 < _entries.Length
            ? _firstObjectOffset + _entries[objectIndex + 1].RelativeOffset
            : _decodedData.Length;
        var value = new PdfObjectParser(_decodedData.AsMemory(start, end - start), parserOptions).Parse();
        if (value is PdfIndirectReference)
        {
            throw new InvalidDataException("An object stream object must not consist solely of an indirect reference");
        }

        return new PdfIndirectObject(requestedObjectId, value);
    }

    private static int ReadHeaderInteger(PdfLexer lexer, string description)
    {
        var token = lexer.Read();
        if (token.Kind != PdfTokenKind.Integer ||
            !int.TryParse(
                token.GetAsciiValue(),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new InvalidDataException($"Expected a 32-bit {description} in the object stream header");
        }

        return value;
    }

    private readonly record struct Entry(int ObjectNumber, int RelativeOffset);
}
