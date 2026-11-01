using ApplicationHelper;
using ApplicationHelper.Utility;
using ApplicationHelper.View.Helper;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Rtpdf.BookPack.Core;
using Rtpdf.BookPack.ViewModels;
using System.Threading.Tasks;

namespace Rtpdf.BookPack.Views;

[HotReloadTrigger]
internal class MainView : Window
{
    private readonly MainViewModel _viewModel;

    public MainView(MainViewModel viewModel)
    {
#if DEBUG
        HotReloadHandler.OnHotReload += OnHotReload;
#endif

        _viewModel = viewModel;
        Content = Render();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

#if DEBUG
        HotReloadHandler.OnHotReload -= OnHotReload;
#endif
    }

    private void OnHotReload()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Content = Render();
        });
    }

    private Control Render()
    {
        return new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowDefinitions = [
                new(GridLength.Star),
                new(GridLength.Auto),
            ],
        }.WithChild(new FileListView(_viewModel.FileListViewModel).Render(), 0, 0)
        .WithChild(RenderFooter(), 1, 0)
        .WithChild(RenderProgress(), 0, 0, 2, 1)
        .WithChild(new ExportOptionView
        {
            OnConfirmed = async option =>
            {
                await SetupAndExport(option);
                _viewModel.IsShowExportOption = false;
            },
            OnCanceled = () => { _viewModel.IsShowExportOption = false; },
        }.Render()
        .With(control =>
        {
            control[!Control.IsVisibleProperty] = CompiledBinding.Create<MainViewModel, bool>(
                source: _viewModel,
                expression: vm => vm.IsShowExportOption
            );
        }), 0, 0, 2, 1);
    }

    private Control RenderFooter()
    {
        return new Border
        {
            Padding = new(4),
            Child = new Grid
            {
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ColumnSpacing = 4,
                ColumnDefinitions = [
                    new(GridLength.Auto),
                    new(GridLength.Auto),
                    new(GridLength.Star),
                    new(GridLength.Auto),
                    new(GridLength.Auto),
                ],
            }.WithChild(new Button
            {
                Content = "名前順ソート",
            }.WithClick((_, _) => _viewModel.FileListViewModel.SortByName()), 0, 0)
            .WithChild(new Button
            {
                Content = "自然順ソート",
            }.WithClick((_, _) => _viewModel.FileListViewModel.SortByNaturalOrder()), 0, 1)
            .WithChild(new Button
            {
                Padding = new(16, 4),
                Content = "Clear",
            }.WithClick((_, _) => _viewModel.FileListViewModel.Clear()), 0, 3)
            .WithChild(new Button
            {
                Padding = new(16, 4),
                Content = "Export",
            }.WithClick((_, _) => _viewModel.IsShowExportOption = true), 0, 4)
        };
    }

    private Control RenderProgress()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = Brushes.LightGray,
        };

        panel.Children.Add(new ProgressBar
        {
            Margin = new(8.0),
            Height = 18.0,
            Minimum = 0.0,
            Maximum = 100.0,
            [!ProgressBar.ValueProperty] = CompiledBinding.Create<MainViewModel, double?>(
                source: _viewModel,
                expression: vm => vm.SaveProgressPercent,
                mode: BindingMode.OneWay
            )
        });

        panel.Children.Add(new TextBlock
        {
            Margin = new(0.0, 0.0, 0.0, 8.0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextAlignment = TextAlignment.Center,
            Text = "5.0%",
            [!TextBlock.TextProperty] = CompiledBinding.Create<MainViewModel, double?>(
                source: _viewModel,
                expression: vm => vm.SaveProgressPercent,
                mode: BindingMode.OneWay,
                converter: new FuncValueConverter<double?, string>(
                    value => $"{(value ?? 0.0):0.00} %"
                )
            )
        });

        return new Border
        {
            Background = new SolidColorBrush(new Color(0xA0, 0x00, 0x00, 0x00)),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = panel,
            [!Border.IsVisibleProperty] = CompiledBinding.Create<MainViewModel, bool>(
                source: _viewModel,
                expression: vm => vm.IsSaving
            ),
        };
    }

    private async Task SetupAndExport(BookPackOptions options)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var file = await topLevel!.StorageProvider.SaveFilePickerAsync(new()
        {
            Title = "Export PDF",
            DefaultExtension = ".pdf",
            FileTypeChoices = [
                new("pdf") {
                    Patterns = ["*.pdf"],
                }
            ],
            ShowOverwritePrompt = true,

        });

        if (file is null)
        {
            return;
        }

        await _viewModel.SaveToFileAsync(file.Path.LocalPath, options);
    }
}
