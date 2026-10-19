using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ApplicationHelper.ViewModel;

public partial class ColumnedListBoxColumnViewModel : ViewModelBase
{
    public string ID { get; init; } = string.Empty;

    [ObservableProperty]
    public partial double Width { get; set; } = 100.0;

    public double MinWidth { get; init; } = 20.0;

    public bool Resizeable { get; init; } = true;
}

public partial class ColumnedListBoxViewModel<Column, Item> : ViewModelBase
    where Column : ColumnedListBoxColumnViewModel
{
    public ObservableCollection<Column> Columns { get; init; } = new ObservableCollection<Column>();
    public ObservableCollection<Item> Items { get; init; } = new ObservableCollection<Item>();

    [ObservableProperty]
    public partial ColumnedListBoxHeaderViewModel? Header { get; set; }
        = new ColumnedListBoxHeaderViewModel.StickyHeader();

    public bool IsHeaderVisible => Header is not null;

    partial void OnHeaderChanged(ColumnedListBoxHeaderViewModel? oldValue, ColumnedListBoxHeaderViewModel? newValue)
    {
        if ((oldValue is null) != (newValue is null))
        {
            OnPropertyChanged(nameof(IsHeaderVisible));
        }
    }
}
