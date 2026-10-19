using ApplicationHelper.View.Helper;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Rtpdf.Inspector.ViewModels.PdfStructure;

namespace Rtpdf.Inspector.Views.PdfStructure.PdfObjectView;

internal class PdfArrayView(PdfArrayViewModel viewModel) : PdfObjectView(viewModel)
{
    private readonly PdfArrayViewModel _viewModel = viewModel;

    public override Control Render()
    {
        var list = new ListBox
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = _viewModel.Items,
            ItemTemplate = new FuncDataTemplate<PdfObjectViewModel>((item, _) =>
            {
                var border = new Border
                {
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new(1),
                    Child = CreateItemValueView(item),
                };

                return border;
            }),
        }.WithStyle(new Style(selector => selector.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(ListBoxItem.PaddingProperty, new Thickness(0)),
                new Setter(ListBoxItem.MarginProperty, new Thickness(0)),
                new Setter(ListBoxItem.MinHeightProperty, 0.0),
                new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
                new Setter(ListBoxItem.VerticalContentAlignmentProperty, VerticalAlignment.Stretch),
            },
        });
        return list;
    }

    private Control CreateItemValueView(PdfObjectViewModel viewModel)
    {
        if (viewModel is PdfCollapsableObjectViewModel collapsable)
        {
            return CreateCollapsableItemValueView(collapsable);
        }
        else
        {
            return new ContentControl
            {
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Content = PdfObjectView.CreateView(viewModel).Render(),
            };
        }
    }
}
