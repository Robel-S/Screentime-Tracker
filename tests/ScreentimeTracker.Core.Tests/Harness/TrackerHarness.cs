using Microsoft.Data.Sqlite;
using ScreentimeTracker.Core;

namespace ScreentimeTracker.Core.Tests.Harness;

/// <summary>
/// Drives the Core facade with scripted system signals, a manually advanced clock and SQLite on a
/// temporary database file. Each test gets its own harness and database.
/// </summary>
public sealed class TrackerHarness : IDisposable
{
    public static readonly DateTime Morning = new(2026, 3, 10, 9, 0, 0);

    private readonly string _directory;

    public TrackerHarness()
    {
        _directory = Path.Combine(Path.GetTempPath(), "screentime-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "screentime.db");
        Tracker = ScreenTimeTracker.Open(_databasePath, Signals, Clock);
    }

    private readonly string _databasePath;
    private FakeClock Clock { get; } = new(Morning);

    public FakeSystemSignals Signals { get; } = new();
    public ScreenTimeTracker Tracker { get; private set; }

    public DateOnly Today => DateOnly.FromDateTime(Clock.Now);

    /// <summary>Advances time in one-second steps, ticking the tracker as the host would.</summary>
    public void RunFor(TimeSpan duration)
    {
        Tracker.Tick();
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += TimeSpan.FromSeconds(1))
        {
            Clock.Advance(TimeSpan.FromSeconds(1));
            Tracker.Tick();
        }
    }

    public void Use(AppIdentity app, TimeSpan duration)
    {
        Signals.BringToFront(app);
        RunFor(duration);
    }

    /// <summary>Shuts the tracker down cleanly and opens a fresh one on the same database.</summary>
    public void Restart()
    {
        Tracker.Dispose();
        Tracker = ScreenTimeTracker.Open(_databasePath, Signals, Clock);
    }

    /// <summary>Abandons the running tracker without shutting it down, as a crash would, and opens a fresh one.</summary>
    public void CrashAndRestart()
    {
        Tracker = ScreenTimeTracker.Open(_databasePath, Signals, Clock);
    }

    public IReadOnlyList<SourceTime> TodaysSources() => Tracker.SourcesOn(Today);

    /// <summary>Today's Screen Time for the App, found by the name the user sees in the list.</summary>
    public TimeSpan TimeFor(AppIdentity app) =>
        TodaysSources().SingleOrDefault(s => s.DisplayName == app.DisplayName)?.ScreenTime ?? TimeSpan.Zero;

    public void Dispose()
    {
        Tracker.Dispose();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }
}
