using ApplicationHelper.ViewModel;
using Rtpdf.BookPack.Models;
using System.IO;

namespace Rtpdf.BookPack.ViewModels;

internal class FileViewModel(ImageFileModel model) : ViewModelBase
{
    public ImageFileModel Model { get; } = model;

    public bool IsErrorFile => !Model.IsValidFile;
    public string FilePath => Model.FilePath;
    public string FileName => Path.GetFileName(Model.FilePath);
    public string Resolution => Model.IsValidFile
        ? $"{Model.ImageWidth} x {Model.ImageHeight}"
        : "---";
}
