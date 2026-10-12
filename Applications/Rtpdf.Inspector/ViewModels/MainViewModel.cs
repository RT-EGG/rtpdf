using ApplicationHelper.ViewModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Rtpdf.Inspector.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Greeting { get; set; } = "Welcome to Avalonia!";
}
