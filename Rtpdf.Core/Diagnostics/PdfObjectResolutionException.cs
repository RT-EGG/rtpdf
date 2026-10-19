using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Diagnostics;

/// <summary>
/// The exception thrown when an indirect PDF object cannot be located or resolved.
/// </summary>
public sealed class PdfObjectResolutionException : IOException
{
    public PdfObjectResolutionException(
        string message,
        PdfObjectId objectId,
        long? offset = null,
        Exception? innerException = null)
        : base(FormatMessage(message, objectId, offset), innerException)
    {
        ObjectId = objectId;
        Offset = offset;
    }

    public PdfObjectId ObjectId { get; }

    public long? Offset { get; }

    private static string FormatMessage(string message, PdfObjectId objectId, long? offset) =>
        offset is null
            ? $"{message} (object {objectId})."
            : $"{message} (object {objectId}, byte offset {offset}).";
}
