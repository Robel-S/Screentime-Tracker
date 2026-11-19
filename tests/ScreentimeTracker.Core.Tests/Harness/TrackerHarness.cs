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

    /// <param name="seedDatabase">Writes a database file at the given path before the tracker opens it, as an older version would have left it.</param>
    /// <param name="start">The local time the clock starts at; <see cref="Morning"/> unless given.</param>
    public TrackerHarness(Action<string>? seedDatabase = null, DateTime? start = null)
    {
        Clock = new FakeClock(start ?? Morning);
        _directory = Path.Combine(Path.GetTempPath(), "screentime-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "screentime.db");
        seedDatabase?.Invoke(_databasePath);
        Signals = new FakeSystemSignals(Clock);
        Tabs = new FakeTabFeed(Clock);
        Tracker = ScreenTimeTracker.Open(_databasePath, Signals, Tabs, Clock);
    }

    private readonly string _databasePath;
    private FakeClock Clock { get; }

    public FakeSystemSignals Signals { get; }
    public FakeTabFeed Tabs { get; }
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

    /// <summary>
    /// The machine sleeps and wakes. No ticks run while it sleeps, and by the first tick after waking
    /// the session is active again.
    /// </summary>
    public void SleepFor(TimeSpan duration) => Clock.Advance(duration);

    public void Use(AppIdentity app, TimeSpan duration)
    {
        Signals.BringToFront(app);
        RunFor(duration);
    }

    /// <summary>Brings the browser to the front with its active tab on the URL, and runs.</summary>
    public void Browse(AppIdentity browser, string? url, TimeSpan duration)
    {
        Tabs.Show(browser.Browser!.Value, url);
        Use(browser, duration);
    }

    /// <summary>Shuts the tracker down cleanly and opens a fresh one on the same database.</summary>
    public void Restart()
    {
        Tracker.Dispose();
        Tracker = ScreenTimeTracker.Open(_databasePath, Signals, Tabs, Clock);
    }

    /// <summary>Abandons the running tracker without shutting it down, as a crash would, and opens a fresh one.</summary>
    public void CrashAndRestart()
    {
        Tracker = ScreenTimeTracker.Open(_databasePath, Signals, Tabs, Clock);
    }

    /// <summary>
    /// A harness on a database as it was before Categories existed, already holding six minutes of
    /// Notepad from earlier today.
    /// </summary>
    public static TrackerHarness WithPreCategoriesDatabase() => new(path =>
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        var start = Morning.AddHours(-1);
        command.CommandText = $"""
            CREATE TABLE sources (
                id INTEGER PRIMARY KEY, kind TEXT NOT NULL, key TEXT NOT NULL, display_name TEXT NOT NULL,
                UNIQUE (kind, key));
            CREATE TABLE time_records (
                id INTEGER PRIMARY KEY, source_id INTEGER NOT NULL REFERENCES sources (id),
                start_time INTEGER NOT NULL, end_time INTEGER NOT NULL);
            INSERT INTO sources (id, kind, key, display_name) VALUES (1, 'App', 'notepad.exe', 'Notepad');
            INSERT INTO time_records (source_id, start_time, end_time)
                VALUES (1, {start.Ticks}, {start.AddMinutes(6).Ticks});
            """;
        command.ExecuteNonQuery();
    });

    public IReadOnlyList<SourceTime> TodaysSources() => Tracker.SourcesOn(Today);

    public DayTotals TodaysTotals() => Tracker.TotalsOn(Today);

    public DateOnly Yesterday => Today.AddDays(-1);

    /// <summary>Screen Time for the App on the Day, found by the name the user sees in the list.</summary>
    public TimeSpan TimeOn(DateOnly day, AppIdentity app) => TimeOn(day, app.DisplayName);

    private TimeSpan TimeOn(DateOnly day, string displayName) =>
        Tracker.SourcesOn(day).SingleOrDefault(s => s.DisplayName == displayName)?.ScreenTime ?? TimeSpan.Zero;

    /// <summary>Sets the Category of the App as the user would, by picking it on the App's row in today's list.</summary>
    public void Categorize(AppIdentity app, Category category) => Categorize(app.DisplayName, category);

    /// <summary>Sets the Category of the Source shown under this name in today's list, such as a Website's hostname.</summary>
    public void Categorize(string displayName, Category category)
    {
        var source = TodaysSources().Single(s => s.DisplayName == displayName);
        Tracker.SetCategory(source.Kind, source.Key, category);
    }

    /// <summary>Today's Screen Time for the App, found by the name the user sees in the list.</summary>
    public TimeSpan TimeFor(AppIdentity app) => TimeFor(app.DisplayName);

    /// <summary>Today's Screen Time for the Source the user sees under this name, such as a Website's hostname.</summary>
    public TimeSpan TimeFor(string displayName) => TimeOn(Today, displayName);

    public void Dispose()
    {
        Tracker.Dispose();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }
}
