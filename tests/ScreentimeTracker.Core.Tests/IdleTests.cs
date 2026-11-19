using ScreentimeTracker.Core.Tests.Harness;

namespace ScreentimeTracker.Core.Tests;

public sealed class IdleTests : IDisposable
{
    private readonly TrackerHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public void Screen_Time_stops_once_there_has_been_no_input_for_longer_than_two_minutes()
    {
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(5));
        _h.Signals.StopInput();
        _h.RunFor(TimeSpan.FromMinutes(30));

        Assert.Equal(TimeSpan.FromMinutes(7), _h.TimeFor(Apps.Notepad));
    }

    [Fact]
    public void The_idle_threshold_defaults_to_two_minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), _h.Tracker.IdleThreshold);
    }

    [Fact]
    public void A_longer_idle_threshold_lets_more_time_without_input_count()
    {
        _h.Tracker.SetIdleThreshold(TimeSpan.FromMinutes(10));
        _h.Signals.StopInput();
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(30));

        Assert.Equal(TimeSpan.FromMinutes(10), _h.TimeFor(Apps.Notepad));
    }

    [Fact]
    public void Lowering_the_idle_threshold_takes_effect_immediately()
    {
        _h.Tracker.SetIdleThreshold(TimeSpan.FromMinutes(10));
        _h.Signals.StopInput();
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(5));
        _h.Tracker.SetIdleThreshold(TimeSpan.FromMinutes(1));
        _h.RunFor(TimeSpan.FromMinutes(5));

        Assert.Equal(TimeSpan.FromMinutes(5), _h.TimeFor(Apps.Notepad));
    }

    [Fact]
    public void The_idle_threshold_is_kept_across_a_restart()
    {
        _h.Tracker.SetIdleThreshold(TimeSpan.FromMinutes(5));
        _h.Restart();

        Assert.Equal(TimeSpan.FromMinutes(5), _h.Tracker.IdleThreshold);
    }

    [Theory]
    [InlineData(-60)]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(4 * 60 * 60 + 1)]
    public void The_idle_threshold_must_be_between_thirty_seconds_and_four_hours(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _h.Tracker.SetIdleThreshold(TimeSpan.FromSeconds(seconds)));
        Assert.Equal(TimeSpan.FromMinutes(2), _h.Tracker.IdleThreshold);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(4 * 60 * 60)]
    public void The_idle_threshold_can_be_set_to_either_end_of_its_range(int seconds)
    {
        _h.Tracker.SetIdleThreshold(TimeSpan.FromSeconds(seconds));

        Assert.Equal(TimeSpan.FromSeconds(seconds), _h.Tracker.IdleThreshold);
    }

    [Fact]
    public void Screen_Time_resumes_on_the_next_input_after_being_Idle()
    {
        _h.Signals.StopInput();
        _h.Use(Apps.Notepad, TimeSpan.FromMinutes(20));
        _h.Signals.ResumeInput();
        _h.RunFor(TimeSpan.FromMinutes(4));

        Assert.Equal(TimeSpan.FromMinutes(6), _h.TimeFor(Apps.Notepad));
    }

    [Fact]
    public void The_user_is_never_Idle_while_the_foreground_window_is_fullscreen()
    {
        _h.Signals.GoFullscreen();
        _h.Signals.StopInput();
        _h.Use(Apps.Spotify, TimeSpan.FromMinutes(45));

        Assert.Equal(TimeSpan.FromMinutes(45), _h.TimeFor(Apps.Spotify));
    }

    [Fact]
    public void Leaving_fullscreen_after_a_long_time_without_input_makes_the_user_Idle_at_once()
    {
        _h.Signals.GoFullscreen();
        _h.Signals.StopInput();
        _h.Use(Apps.Spotify, TimeSpan.FromMinutes(45));
        _h.Signals.LeaveFullscreen();
        _h.RunFor(TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(45), _h.TimeFor(Apps.Spotify));
    }
}
