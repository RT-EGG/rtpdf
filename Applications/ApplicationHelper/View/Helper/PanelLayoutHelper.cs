using Avalonia.Controls;

namespace ApplicationHelper.View.Helper;

public static class PanelLayoutHelper
{
    public static T WithChild<T>(this T panel, Control control) where T : Panel
    {
        panel.Children.Add(control);
        return panel;
    }
}
