using ScreentimeTracker.Core.Tests.Harness;

namespace ScreentimeTracker.Core.Tests;

public sealed class SessionTests : IDisposable
{
    private readonly TrackerHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public void Time_while_the_session_is_locked_is_not_Screen_Time()
    {
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(3));
        _h.Signals.Lock();
        _h.RunFor(TimeSpan.FromMinutes(20));

        Assert.Equal(TimeSpan.FromMinutes(3), _h.TimeFor(Apps.Notepad));
    }

    [Fact]
    public void Unlocking_restarts_Screen_Time()
    {
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(3));
        _h.Signals.Lock();
        _h.RunFor(TimeSpan.FromMinutes(20));
        _h.Signals.Unlock();
        _h.RunFor(TimeSpan.FromMinutes(2));

        Assert.Equal(TimeSpan.FromMinutes(5), _h.TimeFor(Apps.Notepad));
    }

    [Fact]
    public void Time_while_the_machine_sleeps_is_not_Screen_Time_and_tracking_resumes_on_waking()
    {
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(3));
        _h.SleepFor(TimeSpan.FromHours(8));
        _h.RunFor(TimeSpan.FromMinutes(2));

        Assert.Equal(TimeSpan.FromMinutes(5), _h.TimeFor(Apps.Notepad));
    }

    [Fact]
    public void Time_with_the_Screentime_Tracker_in_the_foreground_is_never_Screen_Time()
    {
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(3));
        _h.Use(Apps.ScreentimeTracker, TimeSpan.FromMinutes(10));
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(1));

        var source = Assert.Single(_h.TodaysSources());
        Assert.Equal("Notepad", source.DisplayName);
        Assert.Equal(TimeSpan.FromMinutes(4), source.ScreenTime);
    }
}
