namespace Rtpdf.Core.Objects;

/// <summary>
/// Identifies the concrete kind of a PDF object.
/// </summary>
public enum PdfObjectType
{
    Null,
    Boolean,
    Integer,
    Real,
    Name,
    String,
    Array,
    Dictionary,
    Stream,
    IndirectReference,
}
