namespace Rtpdf.Core.Pages;

/// <summary>
/// A rectangle expressed in PDF default user-space units.
/// </summary>
public readonly record struct PdfRectangle(decimal Left, decimal Bottom, decimal Right, decimal Top)
{
    public decimal Width => Right - Left;

    public decimal Height => Top - Bottom;
}
