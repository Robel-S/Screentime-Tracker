using ScreentimeTracker.Core;

namespace ScreentimeTracker.App;

/// <summary>How Timers are described to the user, the same in the tray and the main window.</summary>
internal static class TimerText
{
    public const string NoTimers = "No Timers running";

    private const string AppName = "Screentime Tracker";

    /// <summary>The longest text a tray icon's tooltip can hold.</summary>
    private const int MaxTooltipLength = 127;

    /// <summary>One Timer's name and where it is, such as "Tea: 4:12 left".</summary>
    public static string Describe(TimerSnapshot timer) => timer.State switch
    {
        TimerState.Running => $"{timer.Name}: {Format(timer.Remaining)} left",
        TimerState.Ringing => $"{timer.Name}: time's up",
        TimerState.Snoozed => $"{timer.Name}: snoozed, rings in {Format(timer.Remaining)}",
        _ => throw new ArgumentOutOfRangeException(nameof(timer), timer.State, null),
    };

    /// <summary>The tray tooltip: the Timer due next, and how many others there are.</summary>
    /// <param name="timers">Every Timer, the one due next first, as the tracker lists them.</param>
    public static string Tooltip(IReadOnlyList<TimerSnapshot> timers)
    {
        if (timers.Count == 0) return AppName;
        var more = timers.Count > 1 ? $" (+{timers.Count - 1} more)" : "";
        var text = $"{AppName} - {Describe(timers[0])}";
        // Keep the "+N more" even when a long label has to be cut short.
        var room = MaxTooltipLength - more.Length;
        return (text.Length > room ? text[..(room - 1)] + "…" : text) + more;
    }

    private static string Format(TimeSpan? remaining)
    {
        // Round up, so the Timer reads 0:01 until it actually runs out.
        var time = TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, (remaining ?? TimeSpan.Zero).TotalSeconds)));
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }
}
