namespace Rtpdf.BookPack.Models;

internal record ImageFileModel(
    string FilePath,
    bool IsValidFile,
    int ImageWidth,
    int ImageHeight
)
{
}