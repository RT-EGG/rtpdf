using ApplicationHelper;
using ApplicationHelper.View.Helper;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Rtpdf.BookPack.ViewModels;

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
        .WithChild(RenderFooter(), 1, 0);
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
            }, 0, 4)
        };
    }
}
