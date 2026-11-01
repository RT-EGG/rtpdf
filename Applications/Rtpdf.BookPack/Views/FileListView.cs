using ApplicationHelper;
using ApplicationHelper.View.Classes;
using ApplicationHelper.View.Helper;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Rtpdf.BookPack.ViewModels;
using System.Linq;
using ListBoxColumnViewModel = Rtpdf.BookPack.ViewModels.FileListViewModel.ColumnViewModel;
using ListBoxView = ApplicationHelper.View.ColumnedListBox<
    Rtpdf.BookPack.ViewModels.FileListViewModel.ListBoxViewModel,
    Rtpdf.BookPack.ViewModels.FileListViewModel.ColumnViewModel,
    Rtpdf.BookPack.ViewModels.FileViewModel
>;
using ListBoxViewModel = Rtpdf.BookPack.ViewModels.FileListViewModel.ListBoxViewModel;

namespace Rtpdf.BookPack.Views;

[HotReloadTrigger]
internal class FileListView(FileListViewModel viewModel)
{
    private readonly FileListViewModel _viewModel = viewModel;

    public Control Render()
    {
        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnDefinitions = [
                new(GridLength.Star),
                new(GridLength.Auto),
            ],
        }
        .WithChild(new ListBoxView(_viewModel.ListBox)
        {
            SelectionMode = SelectionMode.Multiple,
            RowHeight = 18.0,
            CreateHeaderColumnControl = CreateHeaderColumnControl,
            CreateCellControl = CreateCellControl,
        }, 0, 0)
        .WithChild(new TextBlock
        {
            Text = "Drag and drop here",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            FontSize = 24,
            [!TextBlock.IsVisibleProperty] = CompiledBinding.Create<FileListViewModel, int>(
                source: _viewModel,
                expression: vm => vm.ListBox.Items.Count,
                converter: new FuncValueConverter<int, bool>(
                    count => count == 0
                )
            ),
        }, 0, 0, 1, 2);

        DragAndDropHelper.Setup(grid, OnDragOver, OnDrop);

        return grid;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;
        if (e.DataTransfer.TryGetFiles() is not IStorageItem[] files)
        {
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is not IStorageItem[] files)
        {
            return;
        }

        try
        {
            await _viewModel.LoadFilesAsync(files.Select(f => f.Path.LocalPath), _ => { });
        }
        finally
        {
        }
    }

    private Control? CreateHeaderColumnControl(ListBoxViewModel vm, ListBoxColumnViewModel col, INameScope _)
    {
        var ctrl = ListBoxView.CreateDefaultHeaderColumn(vm, col, _);
        ctrl.TextAlignment = TextAlignment.Left;
        return ctrl;
    }

    private Control? CreateCellControl(ListBoxViewModel vm, ListBoxColumnViewModel col, int index, FileViewModel item)
    {
        var ctrl = ListBoxView.CreateDefaultCellControl(vm, col, index, item);
        if (item.IsErrorFile)
        {
            ctrl.WithClass(TextBlockClasses.Foreground.Error);
        }

        switch (col.ID)
        {
            case ListBoxColumnViewModel.ID_FileName:
                ctrl.TextAlignment = TextAlignment.Left;
                ctrl.Text = item.FileName;
                break;
            case ListBoxColumnViewModel.ID_Resolution:
                ctrl.Text = item.Resolution;
                break;
        }

        return ctrl;
    }
}
