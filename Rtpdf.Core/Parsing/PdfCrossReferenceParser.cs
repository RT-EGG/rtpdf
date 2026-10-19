using Rtpdf.Core.CrossReferences;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Tokens;

namespace Rtpdf.Core.Parsing;

/// <summary>
/// Parses conventional <c>xref</c> tables and trailer dictionaries.
/// </summary>
public sealed class PdfCrossReferenceParser
{
    private readonly byte[] _input;
    private readonly PdfCrossReferenceParserOptions _options;
    private int _position;

    public PdfCrossReferenceParser(ReadOnlyMemory<byte> input, PdfCrossReferenceParserOptions? options = null)
    {
        _input = input.ToArray();
        _options = options ?? new PdfCrossReferenceParserOptions();
        _options.Validate();
    }

    public PdfCrossReferenceParser(Stream input, PdfCrossReferenceParserOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        _input = buffer.ToArray();
        _options = options ?? new PdfCrossReferenceParserOptions();
        _options.Validate();
    }

    /// <summary>
    /// Locates the final <c>startxref</c>, follows the conventional <c>/Prev</c> chain, and
    /// returns the effective table with newer entries overriding older entries.
    /// </summary>
    public PdfCrossReferenceTable Parse()
    {
        var startXref = FindStartXref();
        var sections = new List<PdfCrossReferenceSection>();
        var visitedOffsets = new HashSet<long>();
        var effectiveEntries = new SortedDictionary<int, PdfObjectLocation>();
        var crossReferenceOffset = startXref.CrossReferenceOffset;
        var startXrefKeywordOffset = startXref.KeywordOffset;

        while (true)
        {
            if (!visitedOffsets.Add(crossReferenceOffset))
            {
                throw Error("Circular trailer /Prev chain detected", ClampOffset(crossReferenceOffset));
            }

            if (sections.Count >= _options.MaximumCrossReferenceSections)
            {
                throw Error(
                    $"Cross-reference chain exceeds the configured limit of {_options.MaximumCrossReferenceSections} sections",
                    ClampOffset(crossReferenceOffset));
            }

            var section = ParseSectionAtCore(crossReferenceOffset, startXrefKeywordOffset);
            sections.Add(section);
            MergeEffectiveEntries(section, effectiveEntries);

            if (section.PreviousCrossReferenceOffset is not long previousOffset)
            {
                break;
            }

            crossReferenceOffset = previousOffset;
            startXrefKeywordOffset = -1;
        }

        ValidateEffectiveTable(sections[0], effectiveEntries);
        return new PdfCrossReferenceTable(sections, effectiveEntries);
    }

    /// <summary>Parses a conventional table at a known byte offset.</summary>
    public PdfCrossReferenceTable ParseAt(long crossReferenceOffset)
    {
        var section = ParseSectionAtCore(crossReferenceOffset, -1);
        var entries = new SortedDictionary<int, PdfObjectLocation>();
        MergeEffectiveEntries(section, entries);
        return new PdfCrossReferenceTable([section], entries);
    }

    private PdfCrossReferenceSection ParseSectionAtCore(long crossReferenceOffset, int startXrefKeywordOffset)
    {
        if (crossReferenceOffset < 0 || crossReferenceOffset >= _input.Length)
        {
            throw Error("Cross-reference offset is outside the input", ClampOffset(crossReferenceOffset));
        }

        _position = (int)crossReferenceOffset;
        ExpectKeyword("xref"u8, "Expected a conventional xref table");

        var entries = new SortedDictionary<int, PdfObjectLocation>();
        while (true)
        {
            SkipWhitespaceAndComments();
            if (TryConsumeKeyword("trailer"u8))
            {
                break;
            }

            var subsectionOffset = _position;
            var firstObjectNumber = ReadUnsignedInteger("Expected the first object number of an xref subsection");
            var entryCount = ReadUnsignedInteger("Expected the entry count of an xref subsection");

            if (firstObjectNumber > int.MaxValue)
            {
                throw Error("Xref object number exceeds the supported 32-bit range", subsectionOffset);
            }

            if (entryCount > int.MaxValue || entryCount > _options.MaximumEntries - entries.Count)
            {
                throw Error($"Xref table exceeds the configured limit of {_options.MaximumEntries} entries", subsectionOffset);
            }

            if (firstObjectNumber + entryCount > (long)int.MaxValue + 1)
            {
                throw Error("Xref subsection object numbers exceed the supported 32-bit range", subsectionOffset);
            }

            for (var index = 0; index < (int)entryCount; index++)
            {
                var objectNumber = (int)firstObjectNumber + index;
                var entry = ReadEntry(objectNumber);
                if (!entries.TryAdd(objectNumber, entry))
                {
                    throw Error($"Duplicate xref entry for object {objectNumber}", subsectionOffset);
                }
            }
        }

        var trailerKeywordOffset = _position - "trailer"u8.Length;
        SkipWhitespaceAndComments();
        var trailerOffset = _position;
        var trailer = ParseTrailer(trailerOffset);
        var declaredSize = ValidateTrailer(trailer, entries, trailerOffset, crossReferenceOffset);

        return new PdfCrossReferenceSection(
            crossReferenceOffset,
            startXrefKeywordOffset,
            trailerKeywordOffset,
            declaredSize,
            entries,
            trailer);
    }

    private void MergeEffectiveEntries(
        PdfCrossReferenceSection section,
        SortedDictionary<int, PdfObjectLocation> effectiveEntries)
    {
        foreach (var entry in section.Entries)
        {
            if (effectiveEntries.ContainsKey(entry.ObjectNumber))
            {
                continue;
            }

            if (effectiveEntries.Count >= _options.MaximumEntries)
            {
                throw Error(
                    $"Effective xref table exceeds the configured limit of {_options.MaximumEntries} entries",
                    ClampOffset(section.CrossReferenceOffset));
            }

            effectiveEntries.Add(entry.ObjectNumber, entry);
        }
    }

    private void ValidateEffectiveTable(
        PdfCrossReferenceSection newestSection,
        SortedDictionary<int, PdfObjectLocation> effectiveEntries)
    {
        if (effectiveEntries.Count > 0 && effectiveEntries.Keys.Max() >= newestSection.DeclaredSize)
        {
            throw Error(
                "The newest trailer /Size must be greater than every effective xref object number",
                newestSection.TrailerOffset);
        }
    }

    private PdfObjectLocation ReadEntry(int objectNumber)
    {
        SkipWhitespaceAndComments();
        var entryOffset = _position;
        var fieldValue = ReadUnsignedInteger("Expected an xref entry offset");
        SkipWhitespaceAndComments();
        var generationOffset = _position;
        var generationNumber = ReadUnsignedInteger("Expected an xref generation number");

        if (generationNumber > ushort.MaxValue)
        {
            throw Error($"Xref generation number must be between 0 and {ushort.MaxValue}", generationOffset);
        }

        SkipWhitespaceAndComments();
        if (_position >= _input.Length)
        {
            throw Error("Expected an xref entry type", _position);
        }

        var typeOffset = _position;
        var kind = _input[_position++] switch
        {
            (byte)'n' => PdfObjectLocationKind.InUse,
            (byte)'f' => PdfObjectLocationKind.Free,
            _ => throw Error("Xref entry type must be 'n' or 'f'", typeOffset),
        };

        if (_position < _input.Length && !IsWhitespace(_input[_position]) && _input[_position] != (byte)'%')
        {
            throw Error("Xref entry type must be followed by whitespace", _position);
        }

        if (kind == PdfObjectLocationKind.InUse && fieldValue >= (long)_input.Length)
        {
            throw Error("In-use xref entry points outside the input", entryOffset);
        }

        if (kind == PdfObjectLocationKind.Free && fieldValue > int.MaxValue)
        {
            throw Error("Free xref entry points to an unsupported object number", entryOffset);
        }

        return new PdfObjectLocation(objectNumber, fieldValue, (ushort)generationNumber, kind);
    }

    private PdfDictionary ParseTrailer(int trailerOffset)
    {
        if (!MatchesKeyword("<<"u8))
        {
            throw Error("The trailer keyword must be followed by a dictionary", trailerOffset);
        }

        try
        {
            var parser = new PdfObjectParser(_input.AsMemory(trailerOffset), _options.ObjectParserOptions);
            return parser.ReadObject() as PdfDictionary
                ?? throw Error("The trailer must be a PDF dictionary", trailerOffset);
        }
        catch (PdfParseException exception)
        {
            throw Error($"Invalid trailer dictionary: {exception.Message}", trailerOffset + exception.Offset);
        }
        catch (PdfLexerException exception)
        {
            throw Error($"Invalid trailer dictionary: {exception.Message}", trailerOffset + exception.Offset);
        }
    }

    private int ValidateTrailer(
        PdfDictionary trailer,
        SortedDictionary<int, PdfObjectLocation> entries,
        int trailerOffset,
        long crossReferenceOffset)
    {
        if (!trailer.TryGetValue("Size", out var sizeObject) ||
            sizeObject is not PdfInteger sizeInteger ||
            sizeInteger.Value is <= 0 or > int.MaxValue)
        {
            throw Error("Trailer /Size must be a positive integer in the supported range", trailerOffset);
        }

        if (entries.Count > 0 && entries.Keys.Max() >= sizeInteger.Value)
        {
            throw Error("Trailer /Size must be greater than every xref object number", trailerOffset);
        }

        if (trailer.TryGetValue("Prev", out var previousObject))
        {
            if (previousObject is not PdfInteger previousInteger ||
                previousInteger.Value < 0 || previousInteger.Value >= _input.Length)
            {
                throw Error("Trailer /Prev must be a valid byte offset", trailerOffset);
            }

            if (previousInteger.Value == crossReferenceOffset)
            {
                throw Error("Trailer /Prev must not reference the current xref table", trailerOffset);
            }
        }

        return (int)sizeInteger.Value;
    }

    private StartXrefLocation FindStartXref()
    {
        var lowerBound = Math.Max(0, _input.Length - _options.MaximumStartXrefSearchBytes);
        for (var index = _input.Length - "startxref"u8.Length; index >= lowerBound; index--)
        {
            if (!_input.AsSpan(index, "startxref"u8.Length).SequenceEqual("startxref"u8))
            {
                continue;
            }

            var end = index + "startxref"u8.Length;
            if ((index > 0 && !IsWhitespace(_input[index - 1])) ||
                (end < _input.Length && !IsWhitespace(_input[end])))
            {
                continue;
            }

            _position = end;
            SkipWhitespaceOnly();
            var offsetPosition = _position;
            var offset = ReadUnsignedInteger("Expected a byte offset after startxref");
            if (offset >= _input.Length)
            {
                throw Error("startxref points outside the input", offsetPosition);
            }

            SkipWhitespaceOnly();
            if (_options.RequireEndOfFileMarker)
            {
                if (!MatchesKeyword("%%EOF"u8))
                {
                    throw Error("Expected %%EOF after startxref", _position);
                }

                _position += "%%EOF"u8.Length;
                SkipWhitespaceOnly();
                if (_position != _input.Length)
                {
                    throw Error("Unexpected data after %%EOF", _position);
                }
            }

            return new StartXrefLocation(offset, index);
        }

        throw Error($"Could not find startxref in the final {_options.MaximumStartXrefSearchBytes} bytes", lowerBound);
    }

    private long ReadUnsignedInteger(string errorMessage)
    {
        SkipWhitespaceAndComments();
        var start = _position;
        long value = 0;

        while (_position < _input.Length && _input[_position] is >= (byte)'0' and <= (byte)'9')
        {
            var digit = _input[_position++] - (byte)'0';
            if (value > (long.MaxValue - digit) / 10)
            {
                throw Error("Decimal integer exceeds the supported 64-bit range", start);
            }

            value = (value * 10) + digit;
        }

        if (_position == start)
        {
            throw Error(errorMessage, start);
        }

        if (_position < _input.Length && !IsWhitespace(_input[_position]) && _input[_position] != (byte)'%')
        {
            throw Error("Decimal integer must be followed by whitespace", _position);
        }

        return value;
    }

    private void ExpectKeyword(ReadOnlySpan<byte> keyword, string errorMessage)
    {
        if (!TryConsumeKeyword(keyword))
        {
            throw Error(errorMessage, _position);
        }
    }

    private bool TryConsumeKeyword(ReadOnlySpan<byte> keyword)
    {
        if (!MatchesKeyword(keyword))
        {
            return false;
        }

        var end = _position + keyword.Length;
        if (end < _input.Length && !IsWhitespace(_input[end]) && _input[end] != (byte)'%')
        {
            return false;
        }

        _position = end;
        return true;
    }

    private bool MatchesKeyword(ReadOnlySpan<byte> keyword) =>
        _position <= _input.Length - keyword.Length &&
        _input.AsSpan(_position, keyword.Length).SequenceEqual(keyword);

    private int SkipWhitespaceAndComments()
    {
        while (_position < _input.Length)
        {
            if (IsWhitespace(_input[_position]))
            {
                _position++;
                continue;
            }

            if (_input[_position] != (byte)'%')
            {
                break;
            }

            _position++;
            while (_position < _input.Length && _input[_position] is not (byte)'\r' and not (byte)'\n')
            {
                _position++;
            }
        }

        return _position;
    }

    private void SkipWhitespaceOnly()
    {
        while (_position < _input.Length && IsWhitespace(_input[_position]))
        {
            _position++;
        }
    }

    private int ClampOffset(long offset) => (int)Math.Clamp(offset, 0, _input.Length);

    private static bool IsWhitespace(byte value) => value is 0 or 9 or 10 or 12 or 13 or 32;

    private static PdfCrossReferenceException Error(string message, int offset) => new(message, offset);

    private readonly record struct StartXrefLocation(long CrossReferenceOffset, int KeywordOffset);
}
