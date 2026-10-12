using Avalonia.Input;
using Avalonia.Interactivity;

namespace ApplicationHelper.View;

public class DragAndDropHelper
{
    public static void Setup(Interactive control, EventHandler<DragEventArgs> onDragOver, EventHandler<DragEventArgs> onDrop)
    {
        DragDrop.SetAllowDrop(control, true);
        DragDrop.AddDragOverHandler(control, onDragOver);
        DragDrop.AddDropHandler(control, onDrop);
    }
}
