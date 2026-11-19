namespace ScreentimeTracker.App.Charts;

/// <summary>The time axis of a chart: gridlines every <see cref="Step"/>, <see cref="Count"/> of them above zero.</summary>
internal readonly record struct ChartScale(TimeSpan Step, int Count)
{
    private const int MaxGridlines = 5;

    private static readonly TimeSpan[] Steps =
    [
        TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1), TimeSpan.FromHours(2),
        TimeSpan.FromHours(3), TimeSpan.FromHours(4), TimeSpan.FromHours(6), TimeSpan.FromHours(12),
    ];

    /// <summary>The top of the axis.</summary>
    public TimeSpan Max => Step * Count;

    /// <summary>The smallest sensible step that fits <paramref name="longest"/> under a few gridlines.</summary>
    public static ChartScale Fit(TimeSpan longest)
    {
        if (longest <= TimeSpan.Zero) return new ChartScale(TimeSpan.FromHours(1), 1);
        foreach (var step in Steps)
        {
            var count = (int)Math.Ceiling(longest / step);
            if (count <= MaxGridlines) return new ChartScale(step, count);
        }
        var last = Steps[^1];
        return new ChartScale(last, (int)Math.Ceiling(longest / last));
    }
}
