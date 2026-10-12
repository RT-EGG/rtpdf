namespace Rtpdf.Core.Objects;

/// <summary>
/// Base class for every value that can appear as a PDF object.
/// </summary>
public abstract class PdfObject
{
    private protected PdfObject()
    {
    }

    public abstract PdfObjectType Type { get; }
}
