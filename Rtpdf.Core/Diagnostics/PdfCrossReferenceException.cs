namespace Rtpdf.Core.Diagnostics;

/// <summary>
/// The exception thrown when a conventional cross-reference table is malformed.
/// </summary>
public sealed class PdfCrossReferenceException : FormatException
{
    public PdfCrossReferenceException(string message, int offset)
        : base($"{message} (byte offset {offset}).")
    {
        Offset = offset;
    }

    public int Offset { get; }
}
