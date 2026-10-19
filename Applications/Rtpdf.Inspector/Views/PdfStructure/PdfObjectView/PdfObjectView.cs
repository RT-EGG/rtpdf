using ApplicationHelper;
using ApplicationHelper.View.Helper;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using FluentIcons.Avalonia.Fluent;
using FluentIcons.Common;
using Rtpdf.Inspector.ViewModels.PdfStructure;

namespace Rtpdf.Inspector.Views.PdfStructure.PdfObjectView;

[HotReloadTrigger]
internal class PdfObjectView(PdfObjectViewModel viewModel)
{
    private PdfObjectViewModel _viewModel = viewModel;

    public virtual Control Render()
    {
        return new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextAlignment = TextAlignment.Left,
            Text = _viewModel?.AsString() ?? "???",
        };
    }

    public static PdfObjectView CreateView(PdfObjectViewModel viewModel)
    {
        return viewModel switch
        {
            PdfStringViewModel str => new PdfStringView(str),
            PdfArrayViewModel array => new PdfArrayView(array),
            PdfDictionaryViewModel dict => new PdfDictionaryView(dict),
            PdfStreamViewModel stream => new PdfStreamView(stream),
            PdfIndirectReferenceViewModel indirectRef => new PdfIndirectReferenceView(indirectRef),
            _ => new PdfObjectView(viewModel),
        };
    }

    protected Control CreateCollapsableItemValueView(PdfCollapsableObjectViewModel viewModel)
    {
        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowDefinitions = [
                new(GridLength.Auto),
                new(GridLength.Auto),
            ],
            ColumnDefinitions = [
                new(GridLength.Auto),
                new(GridLength.Star),
            ],
            ColumnSpacing = 4.0,
        }.WithChild(new Button
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent,
            Padding = new(2),
            Content = new SymbolIcon
            {
                Width = 8,
                Height = 8,
                FontSize = 12,
                [!SymbolIcon.SymbolProperty] = CompiledBinding.Create<PdfCollapsableObjectViewModel, bool>(
                    source: viewModel,
                    expression: vm => vm.IsExpanded,
                    converter: new FuncValueConverter<bool, Symbol>(isExpanded => isExpanded ? Symbol.ChevronDown : Symbol.ChevronRight)
                ),
            },
        }.WithClick((_, _) => viewModel.IsExpanded = !viewModel.IsExpanded), 0, 0)
        .WithChild(new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextAlignment = TextAlignment.Left,
            Text = viewModel switch
            {
                PdfDictionaryViewModel dict => $"Dictionary ({dict.Items.Count})",
                PdfArrayViewModel array => $"Array ({array.Items.Count})",
                PdfStreamViewModel stream => $"Stream ({stream.DataLength} bytes)",
                PdfIndirectReferenceViewModel reference => $"Indirect Reference {reference.Reference}",
                _ => "???",
            },
        }, 0, 1)
        .WithChild(new ContentControl
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            [!ContentControl.ContentProperty] = CompiledBinding.Create<PdfCollapsableObjectViewModel, bool>(
                source: viewModel,
                expression: vm => vm.IsExpanded,
                converter: new FuncValueConverter<bool, Control?>(isExpanded =>
                    isExpanded ? PdfObjectView.CreateView(viewModel).Render() : null
                )
            ),
            [!ContentControl.IsVisibleProperty] = CompiledBinding.Create<PdfCollapsableObjectViewModel, bool>(
                source: viewModel,
                expression: vm => vm.IsExpanded
            ),
        }, 1, 0, 1, 2);
        return grid;
    }
}
