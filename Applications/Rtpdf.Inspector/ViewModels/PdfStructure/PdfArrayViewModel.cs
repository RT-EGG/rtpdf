using Rtpdf.Core.Objects;
using Rtpdf.Core.Storage;
using System.Collections.Generic;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal class PdfArrayViewModel : PdfCollapsableObjectViewModel
{
    private readonly PdfArray _model;
    public IReadOnlyList<PdfObjectViewModel> Items { get; }

    public PdfArrayViewModel(PdfObjectStore objectStore, PdfArray model) : base(model)
    {
        _model = model;
        var items = new List<PdfObjectViewModel>();
        foreach (var item in _model)
        {
            items.Add(PdfObjectViewModel.Create(objectStore, item));
        }
        Items = items;
    }
}
