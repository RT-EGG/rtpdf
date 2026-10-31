using ApplicationHelper.ViewModel;
using Rtpdf.BookPack.Models;
using System;
using System.IO;

namespace Rtpdf.BookPack.ViewModels;

internal class FileViewModel(ImageFileModel model) : ViewModelBase, IDisposable
{
    private readonly ImageFileModel _model = model;
    private bool _hasDisposed;

    public bool IsErrorFile => _model.Image is null;
    public string FilePath => _model.FilePath;
    public string FileName => Path.GetFileName(_model.FilePath);
    public string Resolution => _model.Image is null
        ? "---"
        : $"{_model.Image.PixelSize.Width} x {_model.Image.PixelSize.Height}";

    protected virtual void Dispose(bool disposing)
    {
        if (!_hasDisposed)
        {
            if (disposing)
            {
                _model.Dispose();
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
