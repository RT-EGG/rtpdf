using System.Globalization;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Tokens;

namespace Rtpdf.Core.Parsing;

/// <summary>
/// Converts PDF lexical tokens into low-level PDF objects.
/// </summary>
public sealed class PdfObjectParser
{
    private readonly PdfLexer _lexer;
    private readonly PdfObjectParserOptions _options;
    private readonly List<PdfToken> _lookahead = [];

    public PdfObjectParser(ReadOnlyMemory<byte> input, PdfObjectParserOptions? options = null)
        : this(new PdfLexer(input), options)
    {
    }

    public PdfObjectParser(Stream input, PdfObjectParserOptions? options = null)
        : this(new PdfLexer(input), options)
    {
    }

    public PdfObjectParser(PdfLexer lexer, PdfObjectParserOptions? options = null)
    {
        _lexer = lexer ?? throw new ArgumentNullException(nameof(lexer));
        _options = options ?? new PdfObjectParserOptions();
        _options.Validate();
    }

    /// <summary>
    /// Gets whether the parser has consumed every token in its input.
    /// </summary>
    public bool IsAtEnd => Peek().Kind == PdfTokenKind.EndOfInput;

    /// <summary>
    /// Parses exactly one PDF object and rejects any trailing tokens.
    /// </summary>
    public PdfObject Parse()
    {
        var result = ReadObject();
        var trailing = Peek();
        if (trailing.Kind != PdfTokenKind.EndOfInput)
        {
            throw Error("Unexpected token after the complete PDF object", trailing);
        }

        return result;
    }

    /// <summary>
    /// Reads the next PDF object, leaving subsequent tokens available for another call.
    /// </summary>
    public PdfObject ReadObject()
    {
        var token = Peek();
        if (token.Kind == PdfTokenKind.EndOfInput)
        {
            throw Error("Expected a PDF object but reached the end of input", token);
        }

        return ParseObject(0);
    }

    internal PdfToken PeekToken(int offset = 0) => Peek(offset);

    internal PdfToken ReadToken() => Consume();

    private PdfObject ParseObject(int nestingDepth)
    {
        var token = Peek();
        return token.Kind switch
        {
            PdfTokenKind.Null => ParseNull(),
            PdfTokenKind.Boolean => ParseBoolean(),
            PdfTokenKind.Integer => ParseIntegerOrIndirectReference(),
            PdfTokenKind.Real => ParseReal(),
            PdfTokenKind.Name => ParseName(),
            PdfTokenKind.LiteralString => ParseString(PdfStringKind.Literal),
            PdfTokenKind.HexadecimalString => ParseString(PdfStringKind.Hexadecimal),
            PdfTokenKind.ArrayStart => ParseArray(nestingDepth),
            PdfTokenKind.DictionaryStart => ParseDictionary(nestingDepth),
            _ => throw Error("Expected a PDF object", token),
        };
    }

    private PdfObject ParseNull()
    {
        Consume();
        return PdfNull.Instance;
    }

    private PdfObject ParseBoolean()
    {
        var token = Consume();
        return PdfBoolean.FromValue(token.ValueBytes.Span.SequenceEqual("true"u8));
    }

    private PdfObject ParseIntegerOrIndirectReference()
    {
        var integerToken = Peek();
        if (Peek(1).Kind == PdfTokenKind.Integer &&
            Peek(2).Kind == PdfTokenKind.Keyword &&
            Peek(2).ValueBytes.Span.SequenceEqual("R"u8))
        {
            var generationToken = Peek(1);
            ParseInt32(integerToken, "Indirect object number", out var objectNumber);
            ParseInt32(generationToken, "Indirect generation number", out var generationNumber);

            if (objectNumber <= 0)
            {
                throw Error("An indirect object number must be greater than zero", integerToken);
            }

            if (generationNumber is < 0 or > ushort.MaxValue)
            {
                throw Error($"An indirect generation number must be between 0 and {ushort.MaxValue}", generationToken);
            }

            Consume();
            Consume();
            Consume();
            return new PdfIndirectReference(objectNumber, generationNumber);
        }

        Consume();
        if (!long.TryParse(
                integerToken.GetAsciiValue(),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw Error("PDF integer is outside the supported 64-bit range", integerToken);
        }

        return new PdfInteger(value);
    }

    private PdfObject ParseReal()
    {
        var token = Consume();
        if (!decimal.TryParse(
                token.GetAsciiValue(),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw Error("PDF real number is outside the supported decimal range", token);
        }

        return new PdfReal(value);
    }

    private PdfObject ParseName()
    {
        var token = Consume();
        return new PdfName(token.ValueBytes.Span);
    }

    private PdfObject ParseString(PdfStringKind kind)
    {
        var token = Consume();
        return new PdfString(token.ValueBytes.Span, kind);
    }

    private PdfObject ParseArray(int nestingDepth)
    {
        var openingToken = Consume();
        EnsureNestingDepth(nestingDepth, openingToken);

        var result = new PdfArray();
        while (true)
        {
            var token = Peek();
            if (token.Kind == PdfTokenKind.ArrayEnd)
            {
                Consume();
                return result;
            }

            if (token.Kind == PdfTokenKind.EndOfInput)
            {
                throw Error("Unterminated PDF array", openingToken);
            }

            if (result.Count >= _options.MaximumArrayItems)
            {
                throw Error($"PDF array exceeds the configured limit of {_options.MaximumArrayItems} items", token);
            }

            result.Add(ParseObject(nestingDepth + 1));
        }
    }

    private PdfObject ParseDictionary(int nestingDepth)
    {
        var openingToken = Consume();
        EnsureNestingDepth(nestingDepth, openingToken);

        var result = new PdfDictionary();
        while (true)
        {
            var keyToken = Peek();
            if (keyToken.Kind == PdfTokenKind.DictionaryEnd)
            {
                Consume();
                return result;
            }

            if (keyToken.Kind == PdfTokenKind.EndOfInput)
            {
                throw Error("Unterminated PDF dictionary", openingToken);
            }

            if (keyToken.Kind != PdfTokenKind.Name)
            {
                throw Error("A PDF dictionary key must be a name object", keyToken);
            }

            if (result.Count >= _options.MaximumDictionaryEntries)
            {
                throw Error(
                    $"PDF dictionary exceeds the configured limit of {_options.MaximumDictionaryEntries} entries",
                    keyToken);
            }

            Consume();
            var key = new PdfName(keyToken.ValueBytes.Span);
            if (result.ContainsKey(key))
            {
                throw Error($"Duplicate PDF dictionary key {key}", keyToken);
            }

            var valueToken = Peek();
            if (valueToken.Kind is PdfTokenKind.DictionaryEnd or PdfTokenKind.EndOfInput)
            {
                throw Error($"PDF dictionary key {key} does not have a value", valueToken);
            }

            result.Add(key, ParseObject(nestingDepth + 1));
        }
    }

    private void EnsureNestingDepth(int nestingDepth, PdfToken token)
    {
        if (nestingDepth >= _options.MaximumNestingDepth)
        {
            throw Error(
                $"PDF object nesting exceeds the configured depth of {_options.MaximumNestingDepth}",
                token);
        }
    }

    private static void ParseInt32(PdfToken token, string description, out int value)
    {
        if (!int.TryParse(
                token.GetAsciiValue(),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value))
        {
            throw Error($"{description} is outside the supported 32-bit range", token);
        }
    }

    private PdfToken Peek(int offset = 0)
    {
        while (_lookahead.Count <= offset)
        {
            _lookahead.Add(_lexer.Read());
        }

        return _lookahead[offset];
    }

    private PdfToken Consume()
    {
        var token = Peek();
        _lookahead.RemoveAt(0);
        return token;
    }

    private static PdfParseException Error(string message, PdfToken token) => new(message, token);
}
