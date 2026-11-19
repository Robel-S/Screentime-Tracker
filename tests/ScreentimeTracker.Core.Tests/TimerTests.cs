using ScreentimeTracker.Core;
using ScreentimeTracker.Core.Tests.Harness;

namespace ScreentimeTracker.Core.Tests;

public sealed class TimerTests : IDisposable
{
    private readonly TrackerHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public void A_running_Timer_shows_the_time_left_and_interrupts_when_it_runs_out()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(25));
        _h.RunFor(TimeSpan.FromMinutes(10));

        Assert.Equal(TimerState.Running, _h.Tracker.TimerState);
        Assert.Equal(TimeSpan.FromMinutes(15), _h.Tracker.TimerRemaining);
        Assert.Empty(_h.Presenter.Calls);

        _h.RunFor(TimeSpan.FromMinutes(15));

        Assert.Equal(TimerState.Ringing, _h.Tracker.TimerState);
        Assert.Equal(["Show"], _h.Presenter.Calls);
    }

    [Fact]
    public void A_cancelled_Timer_never_interrupts()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(5));
        _h.RunFor(TimeSpan.FromMinutes(2));

        _h.Tracker.CancelTimer();
        _h.RunFor(TimeSpan.FromMinutes(10));

        Assert.Equal(TimerState.None, _h.Tracker.TimerState);
        Assert.Null(_h.Tracker.TimerRemaining);
        Assert.Empty(_h.Presenter.Calls);
    }

    [Fact]
    public void Dismissing_the_Interruption_ends_the_Timer()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(1));
        _h.RunFor(TimeSpan.FromMinutes(1));

        _h.Tracker.Dismiss();
        _h.RunFor(TimeSpan.FromMinutes(30));

        Assert.Equal(TimerState.None, _h.Tracker.TimerState);
        Assert.Equal(["Show", "Hide"], _h.Presenter.Calls);
    }

    [Fact]
    public void Snoozing_hides_the_Interruption_and_shows_it_again_after_five_minutes()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(1));
        _h.RunFor(TimeSpan.FromMinutes(1));

        _h.Tracker.Snooze();
        Assert.Equal(TimerState.Snoozed, _h.Tracker.TimerState);
        Assert.False(_h.Presenter.IsShowing);

        _h.RunFor(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        Assert.False(_h.Presenter.IsShowing);

        _h.RunFor(TimeSpan.FromSeconds(1));
        Assert.Equal(TimerState.Ringing, _h.Tracker.TimerState);
        Assert.Equal(["Show", "Hide", "Show"], _h.Presenter.Calls);
    }

    [Fact]
    public void Snooze_can_be_repeated()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(1));
        _h.RunFor(TimeSpan.FromMinutes(1));

        _h.Tracker.Snooze();
        _h.RunFor(TimeSpan.FromMinutes(5));
        _h.Tracker.Snooze();
        _h.RunFor(TimeSpan.FromMinutes(5));

        Assert.Equal(TimerState.Ringing, _h.Tracker.TimerState);
        Assert.Equal(["Show", "Hide", "Show", "Hide", "Show"], _h.Presenter.Calls);
    }

    [Fact]
    public void Only_one_Timer_runs_at_a_time_so_starting_another_replaces_it()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(5));
        _h.RunFor(TimeSpan.FromMinutes(2));

        _h.Tracker.StartTimer(TimeSpan.FromMinutes(10));
        _h.RunFor(TimeSpan.FromMinutes(9));

        Assert.Equal(TimeSpan.FromMinutes(1), _h.Tracker.TimerRemaining);
        Assert.Empty(_h.Presenter.Calls);
    }

    [Fact]
    public void A_new_Timer_can_be_started_from_the_Interruption()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(1));
        _h.RunFor(TimeSpan.FromMinutes(1));

        _h.Tracker.StartTimer(TimeSpan.FromMinutes(25));

        Assert.Equal(TimerState.Running, _h.Tracker.TimerState);
        Assert.Equal(TimeSpan.FromMinutes(25), _h.Tracker.TimerRemaining);
        Assert.Equal(["Show", "Hide"], _h.Presenter.Calls);
    }

    [Fact]
    public void Cancelling_a_Snoozed_Timer_stops_the_Interruption_coming_back()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(1));
        _h.RunFor(TimeSpan.FromMinutes(1));
        _h.Tracker.Snooze();

        _h.Tracker.CancelTimer();
        _h.RunFor(TimeSpan.FromMinutes(10));

        Assert.Equal(TimerState.None, _h.Tracker.TimerState);
        Assert.Equal(["Show", "Hide"], _h.Presenter.Calls);
    }

    [Fact]
    public void The_Timer_runs_through_Idle_and_a_locked_session()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(20));
        _h.Signals.StopInput();
        _h.RunFor(TimeSpan.FromMinutes(10));
        _h.Signals.Lock();
        _h.RunFor(TimeSpan.FromMinutes(10));

        Assert.Equal(TimerState.Ringing, _h.Tracker.TimerState);
        Assert.Equal(["Show"], _h.Presenter.Calls);
    }

    [Fact]
    public void A_Timer_that_runs_out_while_the_machine_sleeps_rings_on_waking()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(5));
        _h.SleepFor(TimeSpan.FromHours(1));
        _h.RunFor(TimeSpan.Zero);

        Assert.Equal(TimerState.Ringing, _h.Tracker.TimerState);
    }

    [Fact]
    public void A_running_Timer_does_not_survive_a_restart()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(5));
        _h.RunFor(TimeSpan.FromMinutes(1));

        _h.Restart();
        _h.RunFor(TimeSpan.FromMinutes(10));

        Assert.Equal(TimerState.None, _h.Tracker.TimerState);
        Assert.Empty(_h.Presenter.Calls);
    }

    [Fact]
    public void A_Timer_must_last_some_time()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _h.Tracker.StartTimer(TimeSpan.Zero));
        Assert.Equal(TimerState.None, _h.Tracker.TimerState);
    }

    [Fact]
    public void Cancelling_a_Ringing_Timer_hides_the_Interruption()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(1));
        _h.RunFor(TimeSpan.FromMinutes(1));

        _h.Tracker.CancelTimer();

        Assert.Equal(TimerState.None, _h.Tracker.TimerState);
        Assert.Equal(["Show", "Hide"], _h.Presenter.Calls);
    }
}
