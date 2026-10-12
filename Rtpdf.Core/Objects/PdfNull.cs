namespace Rtpdf.Core.Objects;

/// <summary>
/// The PDF <c>null</c> object.
/// </summary>
public sealed class PdfNull : PdfObject
{
    private PdfNull()
    {
    }

    public static PdfNull Instance { get; } = new();

    public override PdfObjectType Type => PdfObjectType.Null;

    public override string ToString() => "null";
}
