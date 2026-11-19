using ScreentimeTracker.Core;

namespace ScreentimeTracker.App;

/// <summary>How the Timer is described to the user, the same in the tray and the main window.</summary>
internal static class TimerText
{
    public static string Describe(ScreenTimeTracker tracker) => tracker.TimerState switch
    {
        TimerState.Running => $"Timer: {Format(tracker.TimerRemaining ?? TimeSpan.Zero)} left",
        TimerState.Ringing => "Timer: time's up",
        TimerState.Snoozed => "Timer: snoozed",
        _ => "No Timer running",
    };

    private static string Format(TimeSpan remaining)
    {
        // Round up, so the Timer reads 0:01 until it actually runs out.
        var time = TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, remaining.TotalSeconds)));
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }
}
