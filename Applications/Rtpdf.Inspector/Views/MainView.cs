using ApplicationHelper;
using ApplicationHelper.View;
using Avalonia.Controls;
using Avalonia.Data;
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
    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    public MainView()
    {
#if DEBUG
        HotReloadHandler.OnHotReload += OnHotReload;
#endif

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
        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Spacing = 4.0,

            Children =
            {
                new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    TextAlignment = TextAlignment.Left,
                    FontSize = 24,
                    [!TextBlock.TextProperty] = CompiledBinding.Create<MainViewModel, string>(
                        source: ViewModel,
                        expression: vm => vm.Greeting,
                        mode: BindingMode.OneWay
                    )
                }
            },
        };

        DragAndDropHelper.Setup(panel, OnDragOver, OnDrop);

        return panel;
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
    }
}
