using System.Text;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Filters;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Parsing;
using Rtpdf.Core.Tokens;

namespace Rtpdf.Core.Content;

/// <summary>
/// Parses a decoded PDF content stream into operator invocations without interpreting
/// graphics state or validating operator-specific operand types and arities.
/// </summary>
public sealed class PdfContentStreamParser
{
    private readonly PdfObjectParser _objectParser;
    private readonly PdfContentStreamParserOptions _options;

    public PdfContentStreamParser(
        ReadOnlyMemory<byte> decodedContent,
        PdfContentStreamParserOptions? options = null)
        : this(new PdfLexer(decodedContent), options)
    {
    }

    public PdfContentStreamParser(
        Stream decodedContent,
        PdfContentStreamParserOptions? options = null)
        : this(new PdfLexer(decodedContent), options)
    {
    }

    /// <summary>
    /// Creates a parser after decoding a stream using the supplied filter decoder.
    /// </summary>
    public PdfContentStreamParser(
        PdfStream stream,
        PdfStreamDecoder decoder,
        PdfContentStreamParserOptions? options = null)
        : this(Decode(stream, decoder), options)
    {
    }

    private PdfContentStreamParser(PdfLexer lexer, PdfContentStreamParserOptions? options)
    {
        _options = options ?? new PdfContentStreamParserOptions();
        _options.Validate();
        _objectParser = new PdfObjectParser(
            lexer,
            _options.ObjectParserOptions,
            allowIndirectReferences: false);
    }

    /// <summary>
    /// Parses every operation in the content stream.
    /// </summary>
    public IReadOnlyList<PdfContentOperation> Parse()
    {
        var operations = new List<PdfContentOperation>();
        var operands = new List<PdfObject>();

        while (true)
        {
            PdfToken token;
            try
            {
                token = _objectParser.PeekToken();
            }
            catch (PdfLexerException exception)
            {
                throw Error("Malformed content stream token", exception.Offset, innerException: exception);
            }

            if (token.Kind == PdfTokenKind.EndOfInput)
            {
                if (operands.Count != 0)
                {
                    throw Error("Content stream ended with operands that have no operator", token.Offset);
                }

                return operations.AsReadOnly();
            }

            if (token.Kind == PdfTokenKind.Keyword)
            {
                AddOperation(operations, operands, token);
                continue;
            }

            if (operands.Count >= _options.MaximumOperandsPerOperation)
            {
                throw Error(
                    $"Content stream operation exceeds the configured limit of {_options.MaximumOperandsPerOperation} operands",
                    token.Offset);
            }

            try
            {
                var operand = _objectParser.ReadObject();
                if (operand is PdfIndirectReference)
                {
                    throw Error("Content stream operands must be direct objects", token.Offset);
                }

                operands.Add(operand);
            }
            catch (PdfContentStreamException)
            {
                throw;
            }
            catch (PdfParseException exception)
            {
                throw Error("Malformed content stream operand", exception.Offset, innerException: exception);
            }
            catch (PdfLexerException exception)
            {
                throw Error("Malformed content stream token", exception.Offset, innerException: exception);
            }
        }
    }

    private void AddOperation(
        ICollection<PdfContentOperation> operations,
        List<PdfObject> operands,
        PdfToken token)
    {
        if (operations.Count >= _options.MaximumOperations)
        {
            throw Error(
                $"Content stream exceeds the configured limit of {_options.MaximumOperations} operations",
                token.Offset);
        }

        if (token.ValueBytes.Length == 0 || token.ValueBytes.Length > _options.MaximumOperatorLength)
        {
            throw Error(
                $"Content stream operator length must be between 1 and {_options.MaximumOperatorLength} bytes",
                token.Offset);
        }

        var operatorBytes = token.ValueBytes.Span;
        if (!IsAsciiOperator(operatorBytes))
        {
            throw Error("Content stream operator must contain printable ASCII bytes", token.Offset);
        }

        var operatorName = Encoding.ASCII.GetString(operatorBytes);
        if (operatorName == "BI")
        {
            throw Error("Inline image parsing is not supported", token.Offset, operatorName);
        }

        if (operatorName is "ID" or "EI")
        {
            throw Error("Inline image delimiter appeared outside a supported inline image", token.Offset, operatorName);
        }

        _objectParser.ReadToken();
        operations.Add(new PdfContentOperation(operatorName, operands, token.Offset));
        operands.Clear();
    }

    private static bool IsAsciiOperator(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            if (value is < 0x21 or > 0x7e)
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] Decode(PdfStream stream, PdfStreamDecoder decoder)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(decoder);
        return decoder.Decode(stream);
    }

    private static PdfContentStreamException Error(
        string message,
        int offset,
        string? operatorName = null,
        Exception? innerException = null) =>
        new(message, offset, operatorName, innerException);
}
