using System.Globalization;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Tokens;

namespace Rtpdf.Core.Parsing;

/// <summary>
/// Parses an indirect object declaration beginning at a known byte offset.
/// </summary>
public sealed class PdfIndirectObjectParser
{
    private readonly PdfObjectParserOptions _options;

    public PdfIndirectObjectParser(PdfObjectParserOptions? options = null)
    {
        _options = options ?? new PdfObjectParserOptions();
        _options.Validate();
    }

    public PdfIndirectObject Parse(ReadOnlyMemory<byte> input, int offset = 0)
    {
        var bytes = input.ToArray();
        return Parse(bytes, offset);
    }

    internal PdfIndirectObject Parse(byte[] input, int offset)
        => Parse(input, offset, streamLengthResolver: null);

    internal PdfIndirectObject Parse(
        byte[] input,
        int offset,
        Func<PdfIndirectReference, PdfObject>? streamLengthResolver)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(offset, input.Length);

        var lexer = new PdfLexer(input, offset);
        var objectNumberToken = Expect(lexer.Read(), PdfTokenKind.Integer, "Expected an indirect object number");
        var generationToken = Expect(lexer.Read(), PdfTokenKind.Integer, "Expected an indirect generation number");
        var declarationToken = Expect(lexer.Read(), PdfTokenKind.Keyword, "Expected the obj keyword");

        if (!declarationToken.ValueBytes.Span.SequenceEqual("obj"u8))
        {
            throw Error("Expected the obj keyword", declarationToken);
        }

        var objectNumber = ParseInt32(objectNumberToken, "Indirect object number");
        if (objectNumber <= 0)
        {
            throw Error("An indirect object number must be greater than zero", objectNumberToken);
        }

        var generationNumber = ParseInt32(generationToken, "Indirect generation number");
        if (generationNumber is < 0 or > ushort.MaxValue)
        {
            throw Error(
                $"An indirect generation number must be between 0 and {ushort.MaxValue}",
                generationToken);
        }

        var objectParser = new PdfObjectParser(lexer, _options);
        var value = objectParser.ReadObject();
        var terminator = objectParser.PeekToken();

        if (terminator.Kind == PdfTokenKind.Keyword &&
            terminator.ValueBytes.Span.SequenceEqual("stream"u8))
        {
            if (value is not PdfDictionary dictionary)
            {
                throw Error("The stream keyword must follow a dictionary", terminator);
            }

            return ParseStream(
                input,
                new PdfObjectId(objectNumber, generationNumber),
                dictionary,
                terminator,
                streamLengthResolver);
        }

        if (terminator.Kind != PdfTokenKind.Keyword ||
            !terminator.ValueBytes.Span.SequenceEqual("endobj"u8))
        {
            throw Error("Expected the endobj keyword", terminator);
        }

        objectParser.ReadToken();
        return new PdfIndirectObject(new PdfObjectId(objectNumber, generationNumber), value);
    }

    private PdfIndirectObject ParseStream(
        byte[] input,
        PdfObjectId objectId,
        PdfDictionary dictionary,
        PdfToken streamToken,
        Func<PdfIndirectReference, PdfObject>? streamLengthResolver)
    {
        var length = GetStreamLength(dictionary, streamToken, streamLengthResolver);
        if (length > _options.MaximumEncodedStreamLength)
        {
            throw Error(
                $"Encoded stream exceeds the configured limit of {_options.MaximumEncodedStreamLength} bytes",
                streamToken);
        }

        var dataStart = ReadEndOfLine(input, streamToken.Offset + streamToken.Length, streamToken);
        var dataEnd = (long)dataStart + length;
        if (dataEnd > input.Length)
        {
            throw Error("Stream /Length extends beyond the end of the input", streamToken);
        }

        var endstreamOffset = SkipOptionalEndOfLine(input, (int)dataEnd);
        if (!Matches(input, endstreamOffset, "endstream"u8))
        {
            throw Error("Expected the endstream keyword after the declared stream data", streamToken);
        }

        var afterEndstream = endstreamOffset + "endstream"u8.Length;
        if (afterEndstream >= input.Length)
        {
            throw Error("Expected the endobj keyword after endstream", streamToken);
        }

        var trailingLexer = new PdfLexer(input, afterEndstream);
        var endobj = trailingLexer.Read();
        if (endobj.Kind != PdfTokenKind.Keyword ||
            !endobj.ValueBytes.Span.SequenceEqual("endobj"u8))
        {
            throw Error("Expected the endobj keyword after endstream", endobj);
        }

        var stream = new PdfStream(dictionary, input.AsSpan(dataStart, length));
        return new PdfIndirectObject(objectId, stream);
    }

    private static int GetStreamLength(
        PdfDictionary dictionary,
        PdfToken streamToken,
        Func<PdfIndirectReference, PdfObject>? streamLengthResolver)
    {
        if (!dictionary.TryGetValue("Length", out var lengthObject))
        {
            throw Error("A stream dictionary must contain /Length", streamToken);
        }

        if (lengthObject is PdfIndirectReference reference)
        {
            if (streamLengthResolver is null)
            {
                throw Error(
                    "An indirect stream /Length requires an object resolver",
                    streamToken);
            }

            lengthObject = streamLengthResolver(reference);
        }

        if (lengthObject is not PdfInteger integer || integer.Value is < 0 or > int.MaxValue)
        {
            throw Error("Stream /Length must resolve to a non-negative 32-bit integer", streamToken);
        }

        return (int)integer.Value;
    }

    private static int ReadEndOfLine(byte[] input, int offset, PdfToken streamToken)
    {
        if (offset >= input.Length)
        {
            throw Error("Expected an end-of-line marker around stream data", streamToken);
        }

        if (input[offset] == (byte)'\n')
        {
            return offset + 1;
        }

        if (input[offset] == (byte)'\r')
        {
            return offset + 1 < input.Length && input[offset + 1] == (byte)'\n'
                ? offset + 2
                : offset + 1;
        }

        throw Error("Expected an end-of-line marker around stream data", streamToken);
    }

    private static int SkipOptionalEndOfLine(byte[] input, int offset)
    {
        if (offset >= input.Length)
        {
            return offset;
        }

        if (input[offset] == (byte)'\n')
        {
            return offset + 1;
        }

        if (input[offset] == (byte)'\r')
        {
            return offset + 1 < input.Length && input[offset + 1] == (byte)'\n'
                ? offset + 2
                : offset + 1;
        }

        return offset;
    }

    private static bool Matches(byte[] input, int offset, ReadOnlySpan<byte> value) =>
        offset >= 0 &&
        offset <= input.Length - value.Length &&
        input.AsSpan(offset, value.Length).SequenceEqual(value);

    private static PdfToken Expect(PdfToken token, PdfTokenKind kind, string message)
    {
        if (token.Kind != kind)
        {
            throw Error(message, token);
        }

        return token;
    }

    private static int ParseInt32(PdfToken token, string description)
    {
        if (!int.TryParse(
                token.GetAsciiValue(),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw Error($"{description} is outside the supported 32-bit range", token);
        }

        return value;
    }

    private static PdfParseException Error(string message, PdfToken token) => new(message, token);
}
