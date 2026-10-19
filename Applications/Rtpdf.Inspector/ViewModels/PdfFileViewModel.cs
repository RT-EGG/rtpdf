using CommunityToolkit.Mvvm.ComponentModel;
using Rtpdf.Inspector.Models;
using Rtpdf.Inspector.ViewModels.PdfStructure;

namespace Rtpdf.Inspector.ViewModels;

internal partial class PdfFileViewModel : ObservableObject
{
    private readonly PdfInspectionSession _model;
    public PdfCrossReferenceTableViewModel CrossReferenceTableViewModel { get; }
    public PdfTrailerViewModel TrailerViewModel { get; }

    [ObservableProperty]
    public partial int CurrentIndex { get; set; } = 0;

    public PdfFileViewModel(PdfInspectionSession model)
    {
        _model = model;

        CrossReferenceTableViewModel = new PdfCrossReferenceTableViewModel(_model);
        TrailerViewModel = new PdfTrailerViewModel(_model);
    }
}
