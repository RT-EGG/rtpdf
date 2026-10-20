namespace Rtpdf.Core.Diagnostics;

/// <summary>
/// The exception thrown when PDF encryption metadata, authentication, or encrypted data is invalid or unsupported.
/// </summary>
public sealed class PdfEncryptionException : IOException
{
    public PdfEncryptionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
