using ApplicationHelper.View;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace ApplicationHelper.Theme;

public static class ColumnedListBoxTheme
{
    public const string HeaderSeparatorBrushKey = "ColumnedListBox.HeaderSeparatorBrush";

    public static Styles Create(Palette palette)
    {
        var styles = new Styles();

        // ライトテーマ
        styles.Resources.ThemeDictionaries[ThemeVariant.Light] =
            new ResourceDictionary
            {
                [HeaderSeparatorBrushKey] = palette.LightSeparatorBrush,
            };

        // ダークテーマ
        styles.Resources.ThemeDictionaries[ThemeVariant.Dark] =
            new ResourceDictionary
            {
                [HeaderSeparatorBrushKey] = palette.DarkSeparatorBrush,
            };

        // リソースをColumnedListBoxのStyledPropertyへ接続
        styles.Add(
            new Style(selector =>
                selector.Is<ColumnedListBoxBase>())
            {
                Setters =
                {
                    new Setter(
                        ColumnedListBoxBase.HeaderSeparatorBrushProperty,
                        new DynamicResourceExtension(
                            HeaderSeparatorBrushKey
                        )
                    ),
                    new Setter(
                        ColumnedListBoxBase.HeaderSeparatorThicknessProperty,
                        palette.SeparatorThickness
                    ),
                    new Setter(
                        ColumnedListBoxBase.HeaderResizeHandleWidthProperty,
                        palette.ResizeHandleWidth
                    ),
                },
            }
        );

        return styles;
    }

    public sealed record Palette(
        IBrush LightSeparatorBrush,
        IBrush DarkSeparatorBrush,
        double SeparatorThickness,
        double ResizeHandleWidth
    );

    public class Palettes
    {
        public static Palette Default { get; } = new Palette(
            LightSeparatorBrush: new SolidColorBrush(Color.Parse("#33000000")),
            DarkSeparatorBrush: new SolidColorBrush(Color.Parse("#3DFFFFFF")),
            SeparatorThickness: 2.0,
            ResizeHandleWidth: 8.0
        );
    }
}