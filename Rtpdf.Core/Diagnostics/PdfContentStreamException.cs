namespace Rtpdf.Core.Diagnostics;

/// <summary>
/// The exception thrown when a decoded content stream cannot be parsed into operations.
/// </summary>
public sealed class PdfContentStreamException : FormatException
{
    public PdfContentStreamException(
        string message,
        int offset,
        string? operatorName = null,
        Exception? innerException = null)
        : base(FormatMessage(message, offset, operatorName), innerException)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        Offset = offset;
        Operator = operatorName;
    }

    public int Offset { get; }

    public string? Operator { get; }

    private static string FormatMessage(string message, int offset, string? operatorName) =>
        operatorName is null
            ? $"{message} (byte offset {offset})."
            : $"{message} (operator {operatorName} at byte offset {offset}).";
}
