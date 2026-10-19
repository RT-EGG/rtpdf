using ApplicationHelper;
using ApplicationHelper.View.Helper;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Rtpdf.Inspector.ViewModels;
using System;
using System.Linq;

namespace Rtpdf.Inspector.Views;

[HotReloadTrigger]
internal class MainView : Window
{
    private MainViewModel _viewModel;

    public MainView(MainViewModel viewModel)
    {
#if DEBUG
        HotReloadHandler.OnHotReload += OnHotReload;
#endif

        _viewModel = viewModel;
        Content = RenderContent();
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
            Content = RenderContent();
        });
    }

    private Control RenderContent()
    {
        if (_viewModel.PdfFileViewModel is null)
        {
            var panel = new ContentControl
            {
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = Brushes.Transparent, // D&Dを有効にするために背景をnull以外に設定

                Content = new TextBlock
                {
                    Text = "Drag and drop a PDF file here",
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    FontSize = 24,
                },
            };

            DragAndDropHelper.Setup(panel, OnDragOver, OnDrop);

            return panel;
        }

        var fileView = new PdfFileView(_viewModel.PdfFileViewModel).Render();
        DragAndDropHelper.Setup(fileView, OnDragOver, OnDrop);

        return fileView;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;
        if (e.DataTransfer.TryGetFiles() is not IStorageItem[] files)
        {
            return;
        }

        if (files.Length != 1)
        {
            return;
        }

        var acceptable = files.First().Path.LocalPath.ToLower().EndsWith(".pdf");
        if (acceptable)
        {
            e.DragEffects = DragDropEffects.Copy;
        }

        return;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is not IStorageItem[] files)
        {
            return;
        }

        if (files.Length != 1)
        {
            throw new InvalidProgramException();
        }

        var filePath = files.First().Path.LocalPath;

        try
        {
            await _viewModel.LoadFromFileAsync(filePath);
        }
        finally
        {
            Content = RenderContent();
        }
    }
}
