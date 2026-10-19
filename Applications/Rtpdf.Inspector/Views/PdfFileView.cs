using ApplicationHelper;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Rtpdf.Inspector.ViewModels;
using Rtpdf.Inspector.Views.PdfStructure;

namespace Rtpdf.Inspector.Views;

[HotReloadTrigger]
internal class PdfFileView(PdfFileViewModel viewModel)
{
    private readonly PdfFileViewModel _viewModel = viewModel;

    public Control Render()
    {
        var tabControl = new TabControl
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            [!TabControl.SelectedIndexProperty] = CompiledBinding.Create<PdfFileViewModel, int>(
                source: _viewModel,
                expression: vm => vm.CurrentIndex,
                mode: BindingMode.TwoWay
            )
        };

        tabControl.Items.Add(new TabItem
        {
            Header = new TextBlock
            {
                Text = "XRef",
                FontSize = 16,
            },
            Content = new PdfCrossReferenceTableView(_viewModel.CrossReferenceTableViewModel).Render()
        });

        tabControl.Items.Add(new TabItem
        {
            Header = new TextBlock
            {
                Text = "Trailer",
                FontSize = 16,
            },
            Content = new PdfTrailerView(_viewModel.TrailerViewModel).Render()
        });

        return tabControl;
    }
}
