using ApplicationHelper.Utility;
using Avalonia.Media.Imaging;
using Rtpdf.BookPack.Models;
using Rtpdf.Core.Writing.Content;
using Rtpdf.Core.Writing.Pages;
using System.IO;

namespace Rtpdf.BookPack.Core;

internal class PageBuilder
{
    public readonly PdfPageBuilder _coreBuilder;

    public PageBuilder(PdfPageBuilder coreBuilder)
    {
        _coreBuilder = coreBuilder;
    }

    public void Build(int pageNumber, ImageFileModel image, BookPackOptions options)
    {
        if (!File.Exists(image.FilePath))
        {
            throw new FileNotFoundException($"Image file not found: {image.FilePath}");
        }

        Bitmap bitmap;
        if (options.MaxResolution is null)
        {
            bitmap = new Bitmap(image.FilePath);
        }
        else
        {
            var maxResolution = options.MaxResolution.Value;
            if (image.ImageWidth > maxResolution || image.ImageHeight > maxResolution)
            {
                using var source = File.OpenRead(image.FilePath);

                if (image.ImageWidth > image.ImageHeight)
                {
                    bitmap = Bitmap.DecodeToWidth(
                        source,
                        maxResolution,
                        BitmapInterpolationMode.HighQuality
                    );
                }
                else
                {
                    bitmap = Bitmap.DecodeToHeight(
                        source,
                        maxResolution,
                        BitmapInterpolationMode.HighQuality
                    );
                }
            }
            else
            {
                bitmap = new Bitmap(image.FilePath);
            }
        }

        var imageXObject = bitmap.ToPdfImageXObject(options.JpegQuality);
        _coreBuilder.AddXObject($"Image{pageNumber}", imageXObject);

        _coreBuilder.AddContent(
            new PdfContentStreamBuilder()
                .SaveGraphicsState()
                .ConcatenateMatrix(
                    (decimal)image.ImageWidth.PixelToPoint(), 0,
                    0, (decimal)image.ImageHeight.PixelToPoint(),
                    0, 0)
                .DrawXObject($"Image{pageNumber}")
                .RestoreGraphicsState()
                .Build()
        );

        bitmap.Dispose();
    }
}
