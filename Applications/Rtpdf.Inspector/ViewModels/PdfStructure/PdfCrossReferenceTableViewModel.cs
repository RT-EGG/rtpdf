using ApplicationHelper.ViewModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Rtpdf.Core.CrossReferences;
using Rtpdf.Core.Objects;
using Rtpdf.Inspector.Models;
using System.Linq;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal partial class PdfCrossReferenceTableViewModel : ViewModelBase
{
    private readonly PdfInspectionSession _model;
    public ListBoxViewModel ListBox { get; }

    [ObservableProperty]
    public partial ItemViewModel? SelectedItem { get; set; } = null;
    [ObservableProperty]
    public partial PdfIndirectObjectViewModel? SelectedIndirectObjectViewModel { get; set; } = null;

    public PdfCrossReferenceTableViewModel(PdfInspectionSession model)
    {
        _model = model;
        ListBox = new ListBoxViewModel()
        {
            Columns =
            {
                new() { ID = "ObjectNo", Resizeable = false, Width = 100, },
                new() { ID = "Offset/NextFree", Resizeable = false, Width = 120, },
                new() { ID = "GenNo", Resizeable = false, Width = 80, },
                new() { ID = "Kind", Resizeable = false, Width = 50, },
            },
            Items = [.. _model.CrossReferences.Entries.Select(entry => new ItemViewModel(entry))],
        };
    }

    partial void OnSelectedItemChanged(ItemViewModel? value)
    {
        if (value is null || value.ObjectNumber == 0)
        {
            SelectedIndirectObjectViewModel = null;
            return;
        }

        var indirectObject = _model.ObjectStore.GetIndirectObject(
            new PdfObjectId(
                value.Model.ObjectNumber,
                value.Model.GenerationNumber
            )
        );

        SelectedIndirectObjectViewModel = new(_model.ObjectStore, indirectObject);

        return;
    }

    public class ListBoxViewModel : ColumnedListBoxViewModel<ColumnViewModel, ItemViewModel>
    {
    }

    public class ColumnViewModel : ColumnedListBoxColumnViewModel
    {
    }

    public class ItemViewModel
    {
        public PdfObjectLocation Model;
        public ItemViewModel(PdfObjectLocation model)
        {
            Model = model;
        }

        public int ObjectNumber => Model.ObjectNumber;
        public string ObjectNumberText => ObjectNumber.ToString("D10");
        public int GenerationNumber => Model.GenerationNumber;
        public string GenerationNumberText => GenerationNumber.ToString("D5");
        public string Kind => Model.Kind switch
        {
            PdfObjectLocationKind.InUse => "n",
            PdfObjectLocationKind.Free => "f",
            _ => "?",
        };
        public long OffsetOrNextFree => (Model.ByteOffset ?? Model.NextFreeObjectNumber) ?? 0;
        public string OffsetOrNextFreeText => OffsetOrNextFree.ToString("D10");
    }
}
