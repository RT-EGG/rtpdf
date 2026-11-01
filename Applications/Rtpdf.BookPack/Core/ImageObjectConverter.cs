using Avalonia.Media.Imaging;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Writing.Resources;
using System;
using System.IO;

namespace Rtpdf.BookPack.Core;

internal static class ImageObjectConverter
{
    public static PdfStream ToPdfImageXObject(this Bitmap bitmap, int jpegQuality = 100)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentOutOfRangeException.ThrowIfLessThan(jpegQuality, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(jpegQuality, 100);

        using var encoded = new MemoryStream();

        bitmap.Save(
            encoded,
            new JpegBitmapEncoderOptions
            {
                Quality = jpegQuality,
            });
        encoded.Seek(0, SeekOrigin.Begin);

        return PdfImageXObjectBuilder.FromJpeg(encoded.GetBuffer().AsSpan(0, checked((int)encoded.Length)));
    }
}
