using Avalonia.Media.Imaging;
using System;

namespace Rtpdf.BookPack.Models;

internal record ImageFileModel(
    string FilePath,
    Bitmap? Image
) : IDisposable
{
    private bool _hasDisposed;

    protected virtual void Dispose(bool disposing)
    {
        if (!_hasDisposed)
        {
            if (disposing)
            {
                Image?.Dispose();
            }

            _hasDisposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}