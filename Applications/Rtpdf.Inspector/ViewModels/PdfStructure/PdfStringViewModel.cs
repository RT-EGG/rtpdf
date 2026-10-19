using CommunityToolkit.Mvvm.ComponentModel;
using Rtpdf.Core.Objects;

namespace Rtpdf.Inspector.ViewModels.PdfStructure;

internal partial class PdfStringViewModel : PdfObjectViewModel
{
    private readonly PdfString _model;

    public PdfStringKind Kind => _model.Kind;
    public string? HexString => Kind == PdfStringKind.Hexadecimal ? _model.ToString() : null;
    public string Text => _model.GetText();

    [ObservableProperty]
    public partial bool IsHexStringExpanded { get; set; } = false;
    [ObservableProperty]
    public partial bool IsTextExpanded { get; set; } = false;

    public PdfStringViewModel(PdfString model) : base(model)
    {
        _model = model;
    }
}
