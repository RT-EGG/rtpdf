using ApplicationHelper.View.Helper;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using FluentIcons.Avalonia.Fluent;
using FluentIcons.Common;
using Rtpdf.Core.Objects;
using Rtpdf.Inspector.ViewModels.PdfStructure;

namespace Rtpdf.Inspector.Views.PdfStructure.PdfObjectView;

internal class PdfStringView(PdfStringViewModel viewModel) : PdfObjectView(viewModel)
{
    private PdfStringViewModel _viewModel = viewModel;

    public override Control Render()
    {
        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowDefinitions = [
                new(GridLength.Auto),
                new(GridLength.Auto),
                new(GridLength.Auto),
            ],
        }.WithChild(new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            TextAlignment = TextAlignment.Left,
            Text = _viewModel.Kind switch
            {
                PdfStringKind.Literal => "Literal String",
                PdfStringKind.Hexadecimal => "Hexadecimal String",
                _ => "???"
            },
        }, 0, 0)
        .WithChild(CreateHexStringView(), 1, 0)
        .WithChild(CreateTextView(), 2, 0);

        return base.Render();
    }

    private Control CreateHexStringView()
    {
        var textBlock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Text = _viewModel.HexString,
            TextWrapping = TextWrapping.WrapWithOverflow,
            [!TextBlock.MaxLinesProperty] = CompiledBinding.Create<PdfStringViewModel, bool>(
                source: _viewModel,
                expression: vm => vm.IsHexStringExpanded,
                converter: new FuncValueConverter<bool, int>(expanded => expanded ? int.MaxValue : 1)
            ),
        };
        if (!_viewModel.IsHexStringExpanded)
        {
            ToolTip.SetTip(textBlock, _viewModel.HexString);
        }

        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            [!Grid.IsVisibleProperty] = CompiledBinding.Create<PdfStringViewModel, PdfStringKind>(
                source: _viewModel,
                expression: vm => vm.Kind,
                converter: new FuncValueConverter<PdfStringKind, bool>(kind => kind == PdfStringKind.Hexadecimal)
            ),
        }.WithChild(new Button
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = new SymbolIcon
            {
                [!SymbolIcon.SymbolProperty] = CompiledBinding.Create<PdfStringViewModel, bool>(
                    source: _viewModel,
                    expression: vm => vm.IsHexStringExpanded,
                    converter: new FuncValueConverter<bool, Symbol>(expanded => expanded ? Symbol.ChevronUp : Symbol.ChevronDown)
                ),
            },
        }.WithClick((_, _) => _viewModel.IsHexStringExpanded = !_viewModel.IsHexStringExpanded), 0, 0)
        .WithChild(textBlock, 1, 0);

        return grid;
    }

    private Control CreateTextView()
    {
        var textBlock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Text = _viewModel.Text,
            TextWrapping = TextWrapping.WrapWithOverflow,
            [!TextBlock.MaxLinesProperty] = CompiledBinding.Create<PdfStringViewModel, bool>(
                source: _viewModel,
                expression: vm => vm.IsTextExpanded,
                converter: new FuncValueConverter<bool, int>(expanded => expanded ? int.MaxValue : 1)
            ),
        };
        if (!_viewModel.IsTextExpanded)
        {
            ToolTip.SetTip(textBlock, _viewModel.Text);
        }

        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            [!Grid.IsVisibleProperty] = CompiledBinding.Create<PdfStringViewModel, PdfStringKind>(
                source: _viewModel,
                expression: vm => vm.Kind,
                converter: new FuncValueConverter<PdfStringKind, bool>(kind => kind == PdfStringKind.Hexadecimal)
            ),
        }.WithChild(new Button
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = new SymbolIcon
            {
                [!SymbolIcon.SymbolProperty] = CompiledBinding.Create<PdfStringViewModel, bool>(
                    source: _viewModel,
                    expression: vm => vm.IsTextExpanded,
                    converter: new FuncValueConverter<bool, Symbol>(expanded => expanded ? Symbol.ChevronUp : Symbol.ChevronDown)
                ),
            },
        }.WithClick((_, _) => _viewModel.IsTextExpanded = !_viewModel.IsTextExpanded), 0, 0)
        .WithChild(textBlock, 1, 0);

        return grid;
    }
}
