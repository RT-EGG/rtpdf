using ApplicationHelper.ViewModel;

namespace Rtpdf.BookPack.ViewModels;

internal partial class MainViewModel : ViewModelBase
{
    public FileListViewModel FileListViewModel { get; } = new();


}
