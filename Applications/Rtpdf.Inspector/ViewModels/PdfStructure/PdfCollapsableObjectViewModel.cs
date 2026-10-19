using CommunityToolkit.Mvvm.ComponentModel;
using Rtpdf.Core.Objects;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal partial class PdfCollapsableObjectViewModel : PdfObjectViewModel
{
    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = false;

    public PdfCollapsableObjectViewModel(PdfObject model) : base(model)
    {

    }
}
