namespace Rtpdf.Core.Objects;

/// <summary>
/// Base class for PDF integer and real number objects.
/// </summary>
public abstract class PdfNumber : PdfObject
{
    private protected PdfNumber()
    {
    }

    public abstract decimal DecimalValue { get; }
}
