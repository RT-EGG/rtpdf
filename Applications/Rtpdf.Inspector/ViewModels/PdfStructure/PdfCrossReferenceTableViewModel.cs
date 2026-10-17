using ApplicationHelper.ViewModel;
using Rtpdf.Core.CrossReferences;
using System.Linq;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal class PdfCrossReferenceTableViewModel : ViewModelBase
{
    private readonly PdfCrossReferenceTable _model;
    public ListBoxViewModel ListBox { get; }

    public PdfCrossReferenceTableViewModel(PdfCrossReferenceTable model)
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
            Items = [.. _model.Entries.Select(entry => new ItemViewModel(entry))],
        };
    }

    public class ListBoxViewModel : ColumnedListBoxViewModel<ColumnViewModel, ItemViewModel>
    {
    }

    public class ColumnViewModel : ColumnedListBoxColumnViewModel
    {
    }

    public class ItemViewModel
    {
        private PdfObjectLocation _model;
        public ItemViewModel(PdfObjectLocation model)
        {
            _model = model;
        }

        public string ObjectNumber => _model.ObjectNumber.ToString("D10");
        public string GenerationNumber => _model.GenerationNumber.ToString("D5");
        public string Kind => _model.Kind switch
        {
            PdfObjectLocationKind.InUse => "n",
            PdfObjectLocationKind.Free => "f",
            _ => "?",
        };
        public string OffsetOrNextFree => ((_model.ByteOffset ?? _model.NextFreeObjectNumber) ?? 0).ToString("D10");
    }
}
