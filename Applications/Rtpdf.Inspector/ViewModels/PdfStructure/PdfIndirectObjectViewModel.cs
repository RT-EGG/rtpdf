using ApplicationHelper.ViewModel;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Storage;
using System;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal class PdfIndirectObjectViewModel : ViewModelBase
{
    private readonly PdfIndirectObject _model;
    private Lazy<PdfObjectViewModel> _lazyObjectViewModel;
    public PdfObjectViewModel ObjectViewModel => _lazyObjectViewModel.Value;

    public PdfIndirectObjectViewModel(PdfObjectStore objectStore, PdfIndirectObject model)
    {
        _model = model;
        _lazyObjectViewModel = new Lazy<PdfObjectViewModel>(() => PdfObjectViewModel.Create(objectStore, model.Value));
    }
}
