using ApplicationHelper;
using ApplicationHelper.View.Helper;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Rtpdf.Inspector.ViewModels.PdfStructure;
using System.Linq;

namespace Rtpdf.Inspector.Views.PdfStructure.PdfObjectView;

[HotReloadTrigger]
internal class PdfDictionaryView(PdfDictionaryViewModel viewModel) : PdfObjectView(viewModel)
{
    private readonly PdfDictionaryViewModel _viewModel = viewModel;

    public override Control Render()
    {
        var list = new ListBox
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = _viewModel.Items,
            ItemTemplate = new FuncDataTemplate<(string Key, PdfObjectViewModel Value)>((item, _) =>
            {
                var border = new Border
                {
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new(1),
                    Child = new Grid
                    {
                        ColumnDefinitions = [
                            new(200, GridUnitType.Pixel),
                            new(GridLength.Star),
                        ],
                    }.WithChild(new Border
                    {
                        VerticalAlignment = VerticalAlignment.Stretch,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        BorderBrush = Brushes.Gray,
                        BorderThickness = new(0, 0, 1, 0),
                        Background = Brushes.Transparent,
                        Child = new TextBlock
                        {
                            VerticalAlignment = VerticalAlignment.Top,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            TextAlignment = TextAlignment.Left,
                            Padding = new(1),
                            Text = item.Key,
                        }
                    }.WithTip(item.Key), 0, 0)
                    .WithChild(CreateItemValueView(item.Value), 0, 1),
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
