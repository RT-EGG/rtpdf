namespace Rtpdf.Core.Diagnostics;

/// <summary>Represents an error while serializing a PDF document.</summary>
public sealed class PdfWriteException : IOException
{
    public PdfWriteException(string message)
        : base(message)
    {
    }

    public PdfWriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
