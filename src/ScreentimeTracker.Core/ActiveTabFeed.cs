using System.Collections.Concurrent;

namespace ScreentimeTracker.Core;

/// <summary>
/// The active tab feed the local listener writes to and the tracker reads from. Each report is
/// stamped with the time it arrived. Safe to report from the listener's threads while the tracker reads.
/// </summary>
public sealed class ActiveTabFeed(IClock clock) : IActiveTabFeed
{
    private readonly ConcurrentDictionary<SupportedBrowser, TabReport> _latest = new();

    public TabReport? LatestFrom(SupportedBrowser browser) =>
        _latest.TryGetValue(browser, out var report) ? report : null;

    /// <summary>Records the browser's active tab URL, or null for no Website.</summary>
    public void Report(SupportedBrowser browser, string? url) => _latest[browser] = new TabReport(url, clock.Now);
}
