using ScreentimeTracker.Core.Tests.Harness;

namespace ScreentimeTracker.Core.Tests;

public sealed class PersistenceTests : IDisposable
{
    private readonly TrackerHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public void Todays_totals_survive_a_restart()
    {
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(3));
        _h.Use(Apps.Code, TimeSpan.FromMinutes(5));

        _h.Restart();

        Assert.Equal(TimeSpan.FromMinutes(3), _h.TimeFor(Apps.Notepad));
        Assert.Equal(TimeSpan.FromMinutes(5), _h.TimeFor(Apps.Code));
    }

    [Fact]
    public void Time_after_a_restart_adds_to_the_same_App()
    {
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(3));
        _h.Restart();
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(2));

        var source = Assert.Single(_h.TodaysSources());
        Assert.Equal(TimeSpan.FromMinutes(5), source.ScreenTime);
    }

    [Fact]
    public void A_crash_loses_at_most_a_few_seconds_of_the_App_in_front()
    {
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(4));

        _h.CrashAndRestart();

        var kept = _h.TimeFor(Apps.Notepad);
        Assert.InRange(kept, TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(4));
    }
}
