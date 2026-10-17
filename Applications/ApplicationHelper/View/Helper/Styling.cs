using Avalonia;
using Avalonia.Styling;

namespace ApplicationHelper.View.Helper;

public static class Styling
{
    public static T WithStyle<T>(this T element, Style style) where T : StyledElement
    {
        element.Styles.Add(style);
        return element;
    }
}
