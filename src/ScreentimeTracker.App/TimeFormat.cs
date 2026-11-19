namespace ScreentimeTracker.App;

/// <summary>How amounts of Screen Time are written, the same on every page.</summary>
internal static class TimeFormat
{
    /// <summary>To the minute, for charts and totals: "2h 10m", "2h", "45m", "&lt;1m" or "0m".</summary>
    public static string Short(TimeSpan time)
    {
        if (time <= TimeSpan.Zero) return "0m";
        if (time < TimeSpan.FromMinutes(1)) return "<1m";
        var hours = (int)time.TotalHours;
        if (hours == 0) return $"{time.Minutes}m";
        return time.Minutes == 0 ? $"{hours}h" : $"{hours}h {time.Minutes}m";
    }

    /// <summary>For the Source list: "2h 10m", or "12m 05s" under an hour.</summary>
    public static string Long(TimeSpan time) => time.TotalHours >= 1
        ? $"{(int)time.TotalHours}h {time.Minutes:00}m"
        : $"{time.Minutes}m {time.Seconds:00}s";

    /// <summary>The share <paramref name="part"/> is of <paramref name="whole"/>, as a whole percentage such as "52%".</summary>
    public static string Share(TimeSpan part, TimeSpan whole) =>
        whole > TimeSpan.Zero ? $"{Math.Round(part / whole * 100):0}%" : "0%";

    /// <summary>To the second, for hovering a chart: "2h 10m 05s", "12m 05s" or "5s".</summary>
    public static string Exact(TimeSpan time)
    {
        if (time.TotalHours >= 1) return $"{(int)time.TotalHours}h {time.Minutes:00}m {time.Seconds:00}s";
        return time.TotalMinutes >= 1 ? $"{time.Minutes}m {time.Seconds:00}s" : $"{time.Seconds}s";
    }
}
