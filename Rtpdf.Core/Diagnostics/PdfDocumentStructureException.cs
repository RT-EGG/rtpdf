using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Diagnostics;

/// <summary>
/// The exception thrown when a catalog or page tree violates the expected PDF structure.
/// </summary>
public sealed class PdfDocumentStructureException : IOException
{
    public PdfDocumentStructureException(
        string message,
        PdfObjectId? objectId = null,
        Exception? innerException = null)
        : base(FormatMessage(message, objectId), innerException)
    {
        ObjectId = objectId;
    }

    public PdfObjectId? ObjectId { get; }

    private static string FormatMessage(string message, PdfObjectId? objectId) =>
        objectId is null ? message : $"{message} (object {objectId}).";
}
