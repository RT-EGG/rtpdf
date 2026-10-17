using Avalonia.Controls.Primitives;

namespace ApplicationHelper.View.Helper;

public static class Events
{
    public static T WithTemplateApplied<T>(this T control, EventHandler<TemplateAppliedEventArgs> action)
        where T : TemplatedControl
    {
        control.TemplateApplied += action;
        return control;
    }
}
