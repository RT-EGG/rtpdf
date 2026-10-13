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
            throw Error("Stream objects are not supported by the indirect object parser yet", terminator);
        }

        if (terminator.Kind != PdfTokenKind.Keyword ||
            !terminator.ValueBytes.Span.SequenceEqual("endobj"u8))
        {
            throw Error("Expected the endobj keyword", terminator);
        }

        objectParser.ReadToken();
        return new PdfIndirectObject(new PdfObjectId(objectNumber, generationNumber), value);
    }

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
