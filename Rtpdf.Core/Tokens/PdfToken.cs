using System.Text;

namespace Rtpdf.Core.Tokens;

/// <summary>
/// A token read from a PDF byte sequence.
/// </summary>
public sealed class PdfToken
{
    private readonly ReadOnlyMemory<byte> _valueBytes;

    internal PdfToken(
        PdfTokenKind kind,
        int offset,
        ReadOnlyMemory<byte> rawBytes,
        ReadOnlyMemory<byte>? valueBytes = null)
    {
        Kind = kind;
        Offset = offset;
        RawBytes = rawBytes;
        _valueBytes = valueBytes ?? rawBytes;
    }

    public PdfTokenKind Kind { get; }

    /// <summary>
    /// Gets the zero-based byte offset of the token in the lexer input.
    /// </summary>
    public int Offset { get; }

    public int Length => RawBytes.Length;

    /// <summary>
    /// Gets the exact source bytes, including delimiters and escapes.
    /// </summary>
    public ReadOnlyMemory<byte> RawBytes { get; }

    /// <summary>
    /// Gets decoded bytes for names and strings, or the raw bytes for all other tokens.
    /// </summary>
    public ReadOnlyMemory<byte> ValueBytes => _valueBytes;

    /// <summary>
    /// Returns the token value as an ASCII string. This is intended for keywords and numbers.
    /// </summary>
    public string GetAsciiValue() => Encoding.ASCII.GetString(_valueBytes.Span);

    public override string ToString() => Kind == PdfTokenKind.EndOfInput
        ? $"{Kind} at {Offset}"
        : $"{Kind} '{GetAsciiValue()}' at {Offset}";
}
