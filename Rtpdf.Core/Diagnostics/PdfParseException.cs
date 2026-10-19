using Rtpdf.Core.Tokens;

namespace Rtpdf.Core.Diagnostics;

/// <summary>
/// The exception thrown when a token sequence cannot be interpreted as a PDF object.
/// </summary>
public sealed class PdfParseException : FormatException
{
    public PdfParseException(string message, PdfToken token)
        : base($"{message} (token {token.Kind} at byte offset {token.Offset}).")
    {
        ArgumentNullException.ThrowIfNull(token);
        Offset = token.Offset;
        TokenKind = token.Kind;
    }

    public int Offset { get; }

    public PdfTokenKind TokenKind { get; }
}
