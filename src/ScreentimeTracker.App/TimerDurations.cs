using System.Globalization;

namespace ScreentimeTracker.App;

/// <summary>The Timer durations the user can pick, and how typed minutes are read, the same everywhere a Timer starts.</summary>
internal static class TimerDurations
{
    /// <summary>The longest Timer that can be typed in, in minutes.</summary>
    public const double MaxMinutes = 24 * 60;

    public static readonly IReadOnlyList<TimeSpan> Presets =
        [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(45), TimeSpan.FromHours(1)];

    public static string InvalidMinutesMessage => $"Enter minutes above 0, up to {MaxMinutes:0}";

    /// <summary>Reads typed minutes; null unless above zero and at most <see cref="MaxMinutes"/>.</summary>
    public static TimeSpan? ParseMinutes(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var minutes)
        && minutes > 0 && minutes <= MaxMinutes
            ? TimeSpan.FromMinutes(minutes)
            : null;

    /// <summary>A preset's label, such as "25m" or "1h".</summary>
    public static string ShortLabel(TimeSpan preset) =>
        preset.TotalMinutes < 60 ? $"{preset.TotalMinutes:0}m" : $"{preset.TotalHours:0}h";

    /// <summary>A preset's label in a menu, such as "25 minutes" or "1 hour".</summary>
    public static string LongLabel(TimeSpan preset) =>
        preset.TotalMinutes < 60 ? $"{preset.TotalMinutes:0} minutes"
        : preset.TotalHours == 1 ? "1 hour"
        : $"{preset.TotalHours:0} hours";
}
