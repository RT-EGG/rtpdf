namespace Rtpdf.BookPack.Core;

internal record BookPackOptions(
    int JpegQuality = 100,
    int? MaxResolution = null)
{
}
