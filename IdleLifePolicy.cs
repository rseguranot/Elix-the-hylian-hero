namespace HylianHeroCompanion;

public static class IdleLifePolicy
{
    public const int InitialMinDelayMilliseconds = 3000;
    public const int InitialMaxDelayMilliseconds = 6500;
    public const int MinDelayMilliseconds = 5000;
    public const int MaxDelayMilliseconds = 14000;
    public const int BusyRetryDelayMilliseconds = 1250;

    public static TimeSpan NextDelay(bool initial = false, Random? random = null)
    {
        random ??= Random.Shared;
        var min = initial ? InitialMinDelayMilliseconds : MinDelayMilliseconds;
        var max = initial ? InitialMaxDelayMilliseconds : MaxDelayMilliseconds;
        return TimeSpan.FromMilliseconds(random.Next(min, max + 1));
    }

    public static string? SelectNextClip(
        IEnumerable<string> available,
        string? lastClip,
        Random? random = null)
    {
        random ??= Random.Shared;
        var clips = available
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (clips.Count == 0)
        {
            return null;
        }

        var candidates = clips
            .Where(name => !string.Equals(name, lastClip, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0)
        {
            candidates = clips;
        }

        return candidates[random.Next(candidates.Count)];
    }
}
