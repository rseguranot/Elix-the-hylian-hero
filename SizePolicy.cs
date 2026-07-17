namespace HylianHeroCompanion;

public static class SizePolicy
{
    public const double BaseWidth = 240;
    public const double BaseHeight = 260;
    public const double MinScale = 0.50;
    public const double MaxScale = 1.00;
    public const double SmallScale = 0.60;
    public const double MediumScale = 0.80;
    public const double WheelStep = 0.05;

    public static double Normalize(double? requestedScale)
    {
        if (requestedScale is not double scale || !double.IsFinite(scale))
        {
            return MaxScale;
        }

        return Math.Round(
            Math.Clamp(scale, MinScale, MaxScale),
            2,
            MidpointRounding.AwayFromZero);
    }
}
