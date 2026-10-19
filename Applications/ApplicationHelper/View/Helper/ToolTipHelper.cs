using Avalonia.Controls;

namespace ApplicationHelper.View.Helper;

public static class ToolTipHelper
{
    public static T WithTip<T>(this T control, object? tip) where T : Control
    {
        ToolTip.SetTip(control, tip);
        return control;
    }
}
