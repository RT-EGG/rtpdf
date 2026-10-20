namespace Rtpdf.Core.CrossReferences;

/// <summary>
/// Describes the state of a PDF cross-reference entry.
/// </summary>
public enum PdfObjectLocationKind
{
    Free,
    InUse,
    Compressed,
}
