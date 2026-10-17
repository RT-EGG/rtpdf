using ApplicationHelper;
using Avalonia.Controls;
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

        return tabControl;
    }
}
