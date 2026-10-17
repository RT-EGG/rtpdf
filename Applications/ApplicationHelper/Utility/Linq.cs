namespace ApplicationHelper.Utility;

public static class Linq
{
    public static IEnumerable<(T Item, int Index)> Indexed<T>(this IEnumerable<T> source)
    {
        return source.Select((item, index) => (item, index));
    }
}
