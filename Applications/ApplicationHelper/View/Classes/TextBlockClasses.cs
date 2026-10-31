using Avalonia.Controls;
using Avalonia.Styling;

namespace ApplicationHelper.View.Classes;

public class TextBlockClasses
{
    public const string Title = "title";

    public static void SetupStyles(Styles styles)
    {
        Foreground.SetupStyles(styles);
        SetupTitleStyles(styles);
    }

    private static void SetupTitleStyles(Styles styles)
    {
        styles.Add(new Style(
            selector => selector
                .OfType<TextBlock>()
                .Class(Title))
        {
            Setters =
            {
                new Setter(
                    TextBlock.FontSizeProperty,
                    16.0),
                new Setter(
                    TextBlock.FontWeightProperty,
                    Avalonia.Media.FontWeight.Bold)
            }
        });
    }

    public class Foreground
    {
        public const string Error = "error";
        public const string Secondary = "secondary";
        public const string Disabled = "disabled";
        public const string Link = "link";

        public static void SetupStyles(Styles styles)
        {
            SetupStyles(styles, Error, FluentBrushKeys.Error);
            SetupStyles(styles, Secondary, FluentBrushKeys.Secondary);
            SetupStyles(styles, Disabled, FluentBrushKeys.Disabled);
            SetupStyles(styles, Link, FluentBrushKeys.Link);
        }

        private static void SetupStyles(Styles styles, string className, string brushKey)
        {
            styles.Add(new Style(
                selector => selector
                    .OfType<TextBlock>()
                    .Class(className))
            {
                Setters =
            {
                new Setter(
                    TextBlock.ForegroundProperty,
                    new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(
                        brushKey))
            }
            });
        }
    }

    internal static class FluentBrushKeys
    {
        public const string Error = "SystemControlErrorTextForegroundBrush";

        public const string Secondary = "SystemControlDescriptionTextForegroundBrush";

        public const string Disabled = "SystemControlDisabledBaseMediumLowBrush";

        public const string Link = "SystemControlHyperlinkTextBrush";
    }
}
