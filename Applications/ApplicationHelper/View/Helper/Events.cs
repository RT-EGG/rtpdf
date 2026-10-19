using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace ApplicationHelper.View.Helper;

public static class Events
{
    public static T WithTemplateApplied<T>(this T control, EventHandler<TemplateAppliedEventArgs> action)
        where T : TemplatedControl
    {
        control.TemplateApplied += action;
        return control;
    }

    public static T WithClick<T>(this T button, EventHandler<RoutedEventArgs> action)
        where T : Button
    {
        button.Click += action;
        return button;
    }
}
