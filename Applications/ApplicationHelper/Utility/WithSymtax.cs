namespace ApplicationHelper.Utility;

public static class WithSymtax
{
    public static T With<T>(this T source, Action<T> action)
    {
        action(source);
        return source;
    }
}
