using ApplicationHelper;
using ApplicationHelper.View.Helper;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Rtpdf.BookPack.Core;
using System;

namespace Rtpdf.BookPack.Views;

[HotReloadTrigger]
internal partial class ExportOptionView
{
    public required Action<BookPackOptions> OnConfirmed { get; init; }
    public required Action OnCanceled { get; init; }

    private ViewModel _viewModel = new();

    public Control Render()
    {
        var backdrop = new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(new Color(0xA0, 0x00, 0x00, 0x00)),
            RowDefinitions = [
                new(1.0, GridUnitType.Star),
                new(GridLength.Auto),
                new(1.0, GridUnitType.Star),
            ],
            ColumnDefinitions = [
                new(1.0, GridUnitType.Star),
                new(1.0, GridUnitType.Star) { MinWidth = 400.0 },
                new(1.0, GridUnitType.Star),
            ],
        }.WithChild(new Border
        {
            CornerRadius = new(8),
            Child = CreateControl(),
            Background = Brushes.White,
        }, 1, 1);

        return backdrop;
    }

    private Control CreateControl()
    {
        return new Grid
        {
            Margin = new(8),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowDefinitions = [
                new(GridLength.Auto),
                new(GridLength.Auto),
                new(GridLength.Auto),
            ]
        }.WithChild(CreateQualityControl(), 0, 0)
        .WithChild(CreateMaxResolutionControl(), 1, 0)
        .WithChild(CreateFooter(), 2, 0);
    }

    private Control CreateQualityControl()
    {
        return new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnDefinitions = [
                new(GridLength.Auto),
                new(GridLength.Star),
                new(GridLength.Auto),
            ],
            ColumnSpacing = 4,
        }.WithChild(new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextAlignment = TextAlignment.Center,
            Text = "Jpeg品質",
        }, 0, 0)
        .WithChild(new Slider
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Minimum = 0,
            Maximum = 100,
            LargeChange = 10,
            SmallChange = 1,
            [!Slider.ValueProperty] = CompiledBinding.Create<ViewModel, double>(
                source: _viewModel,
                expression: vm => vm.JpegQuality,
                mode: BindingMode.TwoWay
            )
        }, 0, 1)
        .WithChild(new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,

            [!TextBlock.TextProperty] = CompiledBinding.Create<ViewModel, double>(
                source: _viewModel,
                expression: vm => vm.JpegQuality,
                mode: BindingMode.OneWay,
                converter: new FuncValueConverter<double, string>(v => $"{v:0}%")
            ),
        }, 0, 2);
    }

    private Control CreateMaxResolutionControl()
    {
        return new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnDefinitions = [
                new(GridLength.Auto),
                new(150.0, GridUnitType.Pixel),
            ],

        }.WithChild(new CheckBox
        {
            Margin = new(0, 0, 32.0, 0),
            Content = "解像度を制限する",
            [!CheckBox.IsCheckedProperty] = CompiledBinding.Create<ViewModel, bool>(
                source: _viewModel,
                expression: vm => vm.LimitResolution,
                mode: BindingMode.TwoWay
            ),
        }, 0, 0)
        .WithChild(new NumericUpDown
        {
            Minimum = 0,
            Maximum = decimal.MaxValue,
            FormatString = "#",
            [!NumericUpDown.IsVisibleProperty] = CompiledBinding.Create<ViewModel, bool>(
                source: _viewModel,
                expression: vm => vm.LimitResolution
            ),
            [!NumericUpDown.ValueProperty] = CompiledBinding.Create<ViewModel, decimal>(
                source: _viewModel,
                expression: vm => vm.MaxResolution,
                mode: BindingMode.TwoWay
            ),
        }, 0, 1);
    }

    private Control CreateFooter()
    {
        return new StackPanel
        {
            Margin = new(0, 16.0, 0, 0),
            Orientation = Orientation.Horizontal,
            FlowDirection = FlowDirection.RightToLeft,
            Spacing = 4.0,
        }.WithChild(new Button
        {
            Content = "確定",
        }.WithClick((_, _) => OnConfirmed(_viewModel.CreateOptions())))
        .WithChild(new Button
        {
            Content = "キャンセル",
        }.WithClick((_, _) => OnCanceled()));
    }

    private partial class ViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial double JpegQuality { get; set; } = 100;
        [ObservableProperty]
        public partial bool LimitResolution { get; set; } = false;
        [ObservableProperty]
        public partial decimal MaxResolution { get; set; } = 2048;

        public BookPackOptions CreateOptions()
        {
            return new BookPackOptions
            {
                JpegQuality = (int)JpegQuality,
                MaxResolution = LimitResolution ? (int?)MaxResolution : null,
            };
        }
    }
}
