using CommunityToolkit.Mvvm.ComponentModel;

namespace ApplicationHelper.ViewModel;

public partial class ColumnedListBoxHeaderViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial double Height { get; set; } = 24.0;

    public sealed class StickyHeader : ColumnedListBoxHeaderViewModel
    {
    }

    //public sealed class FloatingHeader : ColumnedListBoxHeaderViewModel
    //{
    //}
}