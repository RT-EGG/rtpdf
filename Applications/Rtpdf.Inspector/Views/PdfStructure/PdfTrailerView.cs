using ApplicationHelper;
using ApplicationHelper.Utility;
using ApplicationHelper.View.Helper;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Rtpdf.Inspector.ViewModels.PdfStructure;

namespace Rtpdf.Inspector.Views.PdfStructure;

[HotReloadTrigger]
internal class PdfTrailerView(PdfTrailerViewModel viewModel)
{
    private readonly PdfTrailerViewModel _viewModel = viewModel;

    public Control Render()
    {
        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        panel.Children.Add(new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextAlignment = TextAlignment.Left,
            Text = "文書情報",
        });

        panel.Children.Add(CreateDocumentInfoView());

        panel.Children.Add(new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextAlignment = TextAlignment.Left,
            Margin = new(0, 16.0),
            Text = "Raw Dictionary"
        });

        panel.Children.Add(
            new PdfObjectView.PdfDictionaryView(_viewModel.RawDictionary).Render()
            .With(view => view.Margin = new(16.0, 0.0, 0.0, 0.0))
        );

        return new ScrollViewer
        {
            Padding = new(0.0, 0.0, 16.0, 0.0),
            Content = panel,
        };
    }

    private Control CreateDocumentInfoView()
    {
        if (_viewModel.DocumentInfo is null)
        {
            return new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextAlignment = TextAlignment.Left,
                Margin = new(16.0, 0.0, 0.0, 0.0),
                Text = "なし",
            };
        }

        return new Grid
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new(16.0, 0.0, 0.0, 0.0),
            RowDefinitions = [
                new(GridLength.Auto),
                new(GridLength.Auto),
                new(GridLength.Auto),
                new(GridLength.Auto),
                new(GridLength.Auto),
                new(GridLength.Auto),
                new(GridLength.Auto),
                new(GridLength.Auto),
                new(GridLength.Auto),
            ],
            ColumnDefinitions = [
                new(100.0, GridUnitType.Pixel),
                new(GridLength.Auto) { MaxWidth = 300.0 },
                new(GridLength.Star),
            ],
        }.WithChild(CreateBorderedTextBlock("タイトル", new(1)), 0, 0)
        .WithChild(CreateBorderedTextBlock(_viewModel.DocumentInfo.Title ?? "---", new(0, 1, 1, 1), 80.0), 0, 1)
        .WithChild(CreateBorderedTextBlock("作成者", new(1, 0, 1, 1)), 1, 0)
        .WithChild(CreateBorderedTextBlock(_viewModel.DocumentInfo.Author ?? "---", new(0, 0, 1, 1), 80.0), 1, 1)
        .WithChild(CreateBorderedTextBlock("概要", new(1, 0, 1, 1)), 2, 0)
        .WithChild(CreateBorderedTextBlock(_viewModel.DocumentInfo.Subject ?? "---", new(0, 0, 1, 1), 80.0), 2, 1)
        .WithChild(CreateBorderedTextBlock("検索用キーワード", new(1, 0, 1, 1)), 3, 0)
        .WithChild(CreateBorderedTextBlock(_viewModel.DocumentInfo.Keywords ?? "---", new(0, 0, 1, 1), 80.0), 3, 1)
        .WithChild(CreateBorderedTextBlock("作成元アプリ", new(1, 0, 1, 1)), 4, 0)
        .WithChild(CreateBorderedTextBlock(_viewModel.DocumentInfo.Creator ?? "---", new(0, 0, 1, 1), 80.0), 4, 1)
        .WithChild(CreateBorderedTextBlock("変換元アプリ", new(1, 0, 1, 1)), 5, 0)
        .WithChild(CreateBorderedTextBlock(_viewModel.DocumentInfo.Producer ?? "---", new(0, 0, 1, 1), 80.0), 5, 1)
        .WithChild(CreateBorderedTextBlock("作成日時", new(1, 0, 1, 1)), 6, 0)
        .WithChild(CreateBorderedTextBlock(_viewModel.DocumentInfo.CreationDate ?? "---", new(0, 0, 1, 1), 80.0), 6, 1)
        .WithChild(CreateBorderedTextBlock("最終更新日時", new(1, 0, 1, 1)), 7, 0)
        .WithChild(CreateBorderedTextBlock(_viewModel.DocumentInfo.ModDate ?? "---", new(0, 0, 1, 1), 80.0), 7, 1)
        .WithChild(CreateBorderedTextBlock("トラッピング", new(1, 0, 1, 1)), 8, 0)
        .WithChild(CreateBorderedTextBlock(_viewModel.DocumentInfo.Trapped ?? "---", new(0, 0, 1, 1), 80.0), 8, 1)
        .WithChild(
            new PdfObjectView.PdfDictionaryView(_viewModel.DocumentInfo.Dictionary).Render()
            .With(view => view.Margin = new(4, 0, 0, 0))
        , 0, 2, 9, 1);
    }

    private Border CreateBorderedTextBlock(string text, Thickness border, double? minWidth = 80.0)
    {
        var textBlock = new TextBlock
        {
            Text = text,
        };

        if (minWidth.HasValue)
        {
            textBlock.MinWidth = minWidth.Value;
        }

        return new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = border,
            Padding = new(2),
            Child = textBlock,
        };
    }
}
