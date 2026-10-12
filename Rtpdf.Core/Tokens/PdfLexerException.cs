namespace Rtpdf.Core.Tokens;

/// <summary>
/// The exception thrown when a malformed lexical construct is encountered.
/// </summary>
public sealed class PdfLexerException : FormatException
{
    public PdfLexerException(string message, int offset)
        : base($"{message} (byte offset {offset}).")
    {
        Offset = offset;
    }

    public int Offset { get; }
}
