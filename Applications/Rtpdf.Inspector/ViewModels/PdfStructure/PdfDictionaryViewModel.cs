using Rtpdf.Core.Objects;
using Rtpdf.Core.Storage;
using System.Collections.Generic;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal partial class PdfDictionaryViewModel : PdfCollapsableObjectViewModel
{
    private readonly PdfDictionary _model;
    public IReadOnlyList<(string Key, PdfObjectViewModel Value)> Items { get; }

    public PdfDictionaryViewModel(PdfObjectStore objectStore, PdfDictionary model) : base(model)
    {
        _model = model;
        var items = new List<(string Key, PdfObjectViewModel Value)>();
        foreach (var item in _model)
        {
            items.Add((item.Key.ToString(), PdfObjectViewModel.Create(objectStore, item.Value)));
        }
        Items = items;
    }
}
