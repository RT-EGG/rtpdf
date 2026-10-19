using Rtpdf.Core.Objects;
using System.Collections.Generic;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal partial class PdfDictionaryViewModel : PdfCollapsableObjectViewModel
{
    private readonly PdfDictionary _model;
    public IReadOnlyList<(string Key, PdfObjectViewModel Value)> Items { get; }

    public PdfDictionaryViewModel(PdfDictionary model) : base(model)
    {
        _model = model;
        var items = new List<(string Key, PdfObjectViewModel Value)>();
        foreach (var item in _model)
        {
            items.Add((item.Key.ToString(), PdfObjectViewModel.Create(item.Value)));
        }
        Items = items;
    }
}
