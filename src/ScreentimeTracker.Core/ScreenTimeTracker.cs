namespace ScreentimeTracker.Core;

/// <summary>Screen Time attributed to one Source over a period.</summary>
/// <param name="Key">What identifies the Source; for an App, its executable.</param>
public sealed record SourceTime(string Key, string DisplayName, TimeSpan ScreenTime);

/// <summary>
/// The Core facade. The host calls <see cref="Tick"/> about once a second; the tracker samples the
/// system signals, accrues Screen Time to the foreground App and persists it to the SQLite database
/// it owns. Time is written when a record closes and flushed every few seconds while it is open, so a
/// crash loses at most <see cref="FlushInterval"/>.
/// </summary>
public sealed class ScreenTimeTracker : IDisposable
{
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    private readonly SqliteStore _store;
    private readonly ISystemSignals _signals;
    private readonly IClock _clock;
    private OpenRecord? _open;

    private ScreenTimeTracker(SqliteStore store, ISystemSignals signals, IClock clock)
    {
        _store = store;
        _signals = signals;
        _clock = clock;
    }

    public static ScreenTimeTracker Open(string databasePath, ISystemSignals signals, IClock clock)
        => new(new SqliteStore(databasePath), signals, clock);

    /// <summary>Samples the system signals and accrues the time since the last tick.</summary>
    public void Tick()
    {
        var now = _clock.Now;
        var app = _signals.ForegroundApp;
        var key = app is null ? null : AppKey(app);

        if (_open is not null && _open.Key == key)
        {
            _open.End = now;
            if (now - _open.FlushedAt >= FlushInterval) Flush();
            return;
        }

        Close(now);
        if (app is not null)
        {
            var sourceId = _store.SaveApp(key!, app.DisplayName);
            _open = new OpenRecord(key!, _store.StartRecord(sourceId, now), now);
        }
    }

    /// <summary>Screen Time per Source on the given Day, most first, including time not yet flushed.</summary>
    public IReadOnlyList<SourceTime> SourcesOn(DateOnly day)
    {
        Flush();
        var start = day.ToDateTime(TimeOnly.MinValue);
        return _store.SourcesBetween(start, start.AddDays(1));
    }

    public void Dispose()
    {
        Flush();
        _store.Dispose();
    }

    /// <summary>An App is identified by its executable, whatever case the system reports it in.</summary>
    private static string AppKey(AppIdentity app) => app.Executable.ToLowerInvariant();

    private void Close(DateTime at)
    {
        if (_open is null) return;
        _open.End = at;
        Flush();
        _open = null;
    }

    private void Flush()
    {
        if (_open is null) return;
        _store.SetRecordEnd(_open.RecordId, _open.End);
        _open.FlushedAt = _open.End;
    }

    private sealed class OpenRecord(string key, long recordId, DateTime start)
    {
        public string Key { get; } = key;
        public long RecordId { get; } = recordId;
        public DateTime End { get; set; } = start;
        public DateTime FlushedAt { get; set; } = start;
    }
}
