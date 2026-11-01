namespace ApplicationHelper.Utility;

public static class UnitConversion
{
    public static double PixelToPoint(this double pixels, double dpi = 96)
    {
        return (pixels / dpi) * 72;
    }

    public static double PointToPixel(this double points, double dpi = 96)
    {
        return (points / 72) * dpi;
    }

    public static double PixelToPoint(this int pixels, double dpi = 96)
    {
        return (pixels / dpi) * 72;
    }

    public static double PointToPixel(this int points, double dpi = 96)
    {
        return (points / 72) * dpi;
    }
}
