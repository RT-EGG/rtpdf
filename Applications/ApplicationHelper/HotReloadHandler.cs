using ApplicationHelper;
using System.Reflection.Metadata;

[assembly: MetadataUpdateHandler(typeof(HotReloadHandler))]

namespace ApplicationHelper;


public static class HotReloadHandler
{
#if DEBUG
    public static event Action? OnHotReload;
#endif

    public static void UpdateApplication(Type[]? updatedTypes)
    {
#if DEBUG
        if (updatedTypes?.Any(type =>
                type.IsDefined(typeof(HotReloadTriggerAttribute), inherit: true)) == true)
        {
            OnHotReload?.Invoke();
        }
#endif
    }
}
