using System.Globalization;

namespace ScreentimeTracker.Core;

/// <summary>Whether a Source is an App or a Website.</summary>
public enum SourceKind
{
    App,
    Website,
}

/// <summary>Screen Time attributed to one Source over a period.</summary>
/// <param name="Key">What identifies the Source: for an App, its executable; for a Website, its hostname.</param>
public sealed record SourceTime(SourceKind Kind, string Key, string DisplayName, TimeSpan ScreenTime);

/// <summary>
/// The Core facade. The host calls <see cref="Tick"/> about once a second; the tracker samples the
/// system signals and the active tab feed, accrues Screen Time to the Source in front (the foreground
/// App, or the Website in its active tab when it is a supported browser) and persists it to the SQLite database
/// it owns. Time is written when a record closes and flushed every few seconds while it is open, so a
/// crash loses at most <see cref="FlushInterval"/>.
/// </summary>
public sealed class ScreenTimeTracker : IDisposable
{
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    /// <summary>The longest gap between ticks that still counts. The host ticks about once a second.</summary>
    public static readonly TimeSpan MaxTickGap = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How old a browser extension's last report can get before it no longer counts, as when the
    /// extension is disabled or not connected. The extension's heartbeat reports every 30 seconds, so
    /// this tolerates one missed heartbeat.
    /// </summary>
    public static readonly TimeSpan MaxTabReportAge = TimeSpan.FromSeconds(75);

    public static readonly TimeSpan DefaultIdleThreshold = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan MinIdleThreshold = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaxIdleThreshold = TimeSpan.FromHours(4);

    private const string IdleThresholdSetting = "idle_threshold_seconds";

    private readonly SqliteStore _store;
    private readonly ISystemSignals _signals;
    private readonly IActiveTabFeed _tabs;
    private readonly IClock _clock;
    private OpenRecord? _open;
    private DateTime? _lastTick;

    private ScreenTimeTracker(SqliteStore store, ISystemSignals signals, IActiveTabFeed tabs, IClock clock)
    {
        _store = store;
        _signals = signals;
        _tabs = tabs;
        _clock = clock;
        IdleThreshold = ReadIdleThreshold(store);
    }

    public static ScreenTimeTracker Open(string databasePath, ISystemSignals signals, IActiveTabFeed tabs, IClock clock)
        => new(new SqliteStore(databasePath), signals, tabs, clock);

    /// <summary>How long without input before the user is Idle. Persisted; defaults to two minutes.</summary>
    public TimeSpan IdleThreshold { get; private set; }

    /// <summary>
    /// Changes the idle threshold from the next tick onward and persists it. It must lie between
    /// <see cref="MinIdleThreshold"/> and <see cref="MaxIdleThreshold"/>.
    /// </summary>
    public void SetIdleThreshold(TimeSpan threshold)
    {
        if (!IsValidIdleThreshold(threshold))
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold,
                $"The idle threshold must be between {MinIdleThreshold} and {MaxIdleThreshold}.");
        }
        _store.WriteSetting(IdleThresholdSetting, threshold.TotalSeconds.ToString(CultureInfo.InvariantCulture));
        IdleThreshold = threshold;
    }

    /// <summary>Samples the system signals and accrues the time since the last tick.</summary>
    public void Tick()
    {
        var now = _clock.Now;

        // No ticks for a long while means the machine was asleep or the host stalled; that gap is
        // not Screen Time, so the open record ends at the last tick before it.
        var missedTicks = _lastTick is not null && now - _lastTick.Value > MaxTickGap;
        _lastTick = now;
        if (missedTicks) EndAtLastTick();

        var userPresent = _signals.IsSessionActive
            && (_signals.ForegroundIsFullscreen || _signals.SinceLastInput <= IdleThreshold);
        var app = userPresent ? CountedForegroundApp() : null;
        var source = app is null ? null : SourceInFront(app, now);

        if (_open is not null && _open.Source == source)
        {
            _open.End = now;
            if (now - _open.FlushedAt >= FlushInterval) Flush();
            return;
        }

        // When the user moves to another window, the time since the last tick goes to the App they
        // left. When they go Idle or the session locks, it isn't Screen Time.
        if (userPresent) EndAt(now);
        else EndAtLastTick();

        if (source is not null)
        {
            var sourceId = _store.SaveSource(source.Kind, source.Key, source.DisplayName);
            _open = new OpenRecord(source, _store.StartRecord(sourceId, now), now);
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

    private static bool IsValidIdleThreshold(TimeSpan threshold) =>
        threshold >= MinIdleThreshold && threshold <= MaxIdleThreshold;

    private static TimeSpan ReadIdleThreshold(SqliteStore store) =>
        double.TryParse(store.ReadSetting(IdleThresholdSetting), NumberStyles.Float, CultureInfo.InvariantCulture,
            out var seconds) && IsValidIdleThreshold(TimeSpan.FromSeconds(seconds))
            ? TimeSpan.FromSeconds(seconds)
            : DefaultIdleThreshold;

    /// <summary>The foreground App, unless there is none or it is the Screentime Tracker itself.</summary>
    private AppIdentity? CountedForegroundApp()
    {
        var app = _signals.ForegroundApp;
        return app is null || app.IsTracker ? null : app;
    }

    /// <summary>
    /// The Source the foreground App's time goes to: the Website in its active tab when it is a
    /// supported browser with a fresh report of one, otherwise the App itself.
    /// </summary>
    private Source SourceInFront(AppIdentity app, DateTime now)
    {
        var report = app.Browser is { } browser ? _tabs.LatestFrom(browser) : null;
        var url = report is not null && now - report.ReportedAt < MaxTabReportAge ? report.Url : null;
        var website = url is null ? null : Website.FromUrl(url);
        return website is not null
            ? new Source(SourceKind.Website, website, website)
            // An App is identified by its executable, whatever case the system reports it in.
            : new Source(SourceKind.App, app.Executable.ToLowerInvariant(), app.DisplayName);
    }

    private void EndAt(DateTime end)
    {
        if (_open is null) return;
        _open.End = end;
        EndAtLastTick();
    }

    private void EndAtLastTick()
    {
        if (_open is null) return;
        Flush();
        _open = null;
    }

    private void Flush()
    {
        if (_open is null) return;
        _store.SetRecordEnd(_open.RecordId, _open.End);
        _open.FlushedAt = _open.End;
    }

    /// <summary>A Source as the tracker attributes time to it. Two are the same Source when kind and key match.</summary>
    private sealed record Source(SourceKind Kind, string Key, string DisplayName)
    {
        public bool Equals(Source? other) => other is not null && Kind == other.Kind && Key == other.Key;
        public override int GetHashCode() => HashCode.Combine(Kind, Key);
    }

    private sealed class OpenRecord(Source source, long recordId, DateTime start)
    {
        public Source Source { get; } = source;
        public long RecordId { get; } = recordId;
        public DateTime End { get; set; } = start;
        public DateTime FlushedAt { get; set; } = start;
    }
}
