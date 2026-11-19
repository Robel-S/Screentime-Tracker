using ScreentimeTracker.Core;

namespace ScreentimeTracker.Core.Tests.Harness;

/// <summary>
/// Active tab reports the test scripts by hand. Once a browser shows a tab, its extension keeps
/// reporting it (as the heartbeat would) until the test says it stops.
/// </summary>
public sealed class FakeTabFeed(FakeClock clock) : IActiveTabFeed
{
    private readonly Dictionary<SupportedBrowser, (string? Url, DateTime? StoppedAt)> _tabs = new();

    public TabReport? LatestFrom(SupportedBrowser browser) =>
        _tabs.TryGetValue(browser, out var tab) ? new TabReport(tab.Url, tab.StoppedAt ?? clock.Now) : null;

    /// <summary>The browser's active tab shows the URL, or no Website when it is null.</summary>
    public void Show(SupportedBrowser browser, string? url) => _tabs[browser] = (url, null);

    /// <summary>The browser's extension stops reporting from now on, as when it is disabled.</summary>
    public void StopReporting(SupportedBrowser browser) => _tabs[browser] = _tabs[browser] with { StoppedAt = clock.Now };
}
