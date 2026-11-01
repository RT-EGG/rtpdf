using ApplicationHelper.Utility;
using Rtpdf.BookPack.Models;
using Rtpdf.Core.Pages;
using Rtpdf.Core.Writing;
using Rtpdf.Core.Writing.Pages;
using System;
using System.Threading.Tasks;

namespace Rtpdf.BookPack.Core;

internal class BookPacker
{
    public async Task PackImagesToFile(string filePath, ImageFileModel[] images, BookPackOptions options, Action<double> onProgress)
    {
        var docBuilder = new PdfDocumentBuilder();
        var pagesBuilder = new PdfPageTreeBuilder(docBuilder);

        foreach (var (image, i) in images.Indexed())
        {
            var mediaBox = new PdfRectangle(
                0,
                0,
                (decimal)image.ImageWidth.PixelToPoint(),
                (decimal)image.ImageHeight.PixelToPoint()
            );
            var pageBuilder = pagesBuilder.AddPage(mediaBox);
            var builder = new PageBuilder(pageBuilder);
            builder.Build(i, image, options);

            onProgress?.Invoke((double)pagesBuilder.Count / images.Length * 95.0);
        }

        PdfDocumentWriter.Write(filePath, docBuilder);
        onProgress?.Invoke(100.0);
    }
}
