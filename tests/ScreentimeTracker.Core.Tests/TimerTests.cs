using ScreentimeTracker.Core;
using ScreentimeTracker.Core.Tests.Harness;

namespace ScreentimeTracker.Core.Tests;

public sealed class TimerTests : IDisposable
{
    private readonly TrackerHarness _h = new();

    public void Dispose() => _h.Dispose();

    private TimerSnapshot Timer(TimerId id) => _h.Tracker.Timers.Single(t => t.Id == id);

    [Fact]
    public void Several_Timers_run_at_once_and_each_rings_when_it_runs_out()
    {
        var tea = _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Tea");
        var focus = _h.Tracker.StartTimer(TimeSpan.FromMinutes(25), "Focus");
        _h.RunFor(TimeSpan.FromMinutes(3));

        Assert.Equal(TimeSpan.FromMinutes(2), Timer(tea).Remaining);
        Assert.Equal(TimeSpan.FromMinutes(22), Timer(focus).Remaining);
        Assert.Empty(_h.Presenter.Calls);

        _h.RunFor(TimeSpan.FromMinutes(2));

        Assert.Equal(TimerState.Ringing, Timer(tea).State);
        Assert.Equal(TimerState.Running, Timer(focus).State);
        Assert.Equal(TimeSpan.FromMinutes(20), Timer(focus).Remaining);
        Assert.Equal(["Ring: Tea"], _h.Presenter.Calls);
    }

    [Fact]
    public void A_Timer_without_a_label_goes_by_its_duration()
    {
        var labelled = _h.Tracker.StartTimer(TimeSpan.FromMinutes(25), "Write report");
        var plain = _h.Tracker.StartTimer(TimeSpan.FromMinutes(25));
        var blank = _h.Tracker.StartTimer(TimeSpan.FromMinutes(90), "  ");
        var hour = _h.Tracker.StartTimer(TimeSpan.FromHours(1));

        Assert.Equal("Write report", Timer(labelled).Name);
        Assert.Equal("25-minute Timer", Timer(plain).Name);
        Assert.Null(Timer(blank).Label);
        Assert.Equal("90-minute Timer", Timer(blank).Name);
        Assert.Equal("1-hour Timer", Timer(hour).Name);
    }

    [Fact]
    public void Cancelling_one_Timer_leaves_the_others_running()
    {
        var tea = _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Tea");
        var focus = _h.Tracker.StartTimer(TimeSpan.FromMinutes(25), "Focus");
        _h.RunFor(TimeSpan.FromMinutes(2));

        _h.Tracker.CancelTimer(tea);
        _h.RunFor(TimeSpan.FromMinutes(10));

        Assert.Equal([focus], _h.Tracker.Timers.Select(t => t.Id));
        Assert.Equal(TimeSpan.FromMinutes(13), Timer(focus).Remaining);
        Assert.Empty(_h.Presenter.Calls);
    }

    [Fact]
    public void Starting_a_Timer_never_replaces_another()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Tea");
        _h.RunFor(TimeSpan.FromMinutes(2));
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(10), "Focus");
        _h.RunFor(TimeSpan.FromMinutes(3));

        Assert.Equal(["Ring: Tea"], _h.Presenter.Calls);
        Assert.Equal(2, _h.Tracker.Timers.Count);
    }

    [Fact]
    public void Timers_that_ring_together_are_both_listed_on_one_Interruption()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Tea");
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Laundry");
        _h.RunFor(TimeSpan.FromMinutes(5));

        Assert.Equal(["Ring: Tea, Laundry"], _h.Presenter.Calls);
    }

    [Fact]
    public void Each_Timer_that_starts_ringing_calls_attention_again_and_joins_the_list()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Tea");
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(7), "Laundry");
        _h.RunFor(TimeSpan.FromMinutes(7));

        Assert.Equal(["Ring: Tea", "Ring: Tea, Laundry"], _h.Presenter.Calls);
    }

    [Fact]
    public void The_Interruption_hides_only_once_no_Timer_is_ringing()
    {
        var tea = _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Tea");
        var laundry = _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Laundry");
        _h.RunFor(TimeSpan.FromMinutes(5));

        _h.Tracker.Dismiss(tea);
        Assert.True(_h.Presenter.IsShowing);
        Assert.Equal(["Laundry"], _h.Presenter.Listed);

        _h.Tracker.Dismiss(laundry);
        Assert.False(_h.Presenter.IsShowing);
        Assert.Empty(_h.Tracker.Timers);
        Assert.Equal(["Ring: Tea, Laundry", "Update: Laundry", "Hide"], _h.Presenter.Calls);
    }

    [Fact]
    public void Cancelling_a_running_Timer_leaves_the_Interruption_as_it_is()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Tea");
        var focus = _h.Tracker.StartTimer(TimeSpan.FromMinutes(25), "Focus");
        _h.RunFor(TimeSpan.FromMinutes(5));

        _h.Tracker.CancelTimer(focus);

        Assert.Equal(["Ring: Tea"], _h.Presenter.Calls);
    }

    [Fact]
    public void Snoozing_one_Timer_leaves_another_ringing()
    {
        var tea = _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Tea");
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Laundry");
        _h.RunFor(TimeSpan.FromMinutes(5));

        _h.Tracker.Snooze(tea);
        Assert.Equal(TimerState.Snoozed, Timer(tea).State);
        Assert.Equal(["Laundry"], _h.Presenter.Listed);

        _h.RunFor(TimeSpan.FromMinutes(5));
        Assert.Equal(TimerState.Ringing, Timer(tea).State);
        Assert.Equal(["Ring: Tea, Laundry", "Update: Laundry", "Ring: Laundry, Tea"], _h.Presenter.Calls);
    }

    [Fact]
    public void Snoozing_hides_a_Timer_for_five_minutes_and_it_rings_again()
    {
        var tea = _h.Tracker.StartTimer(TimeSpan.FromMinutes(1), "Tea");
        _h.RunFor(TimeSpan.FromMinutes(1));

        _h.Tracker.Snooze(tea);
        Assert.False(_h.Presenter.IsShowing);
        Assert.Equal(TimeSpan.FromMinutes(5), Timer(tea).Remaining);

        _h.RunFor(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        Assert.False(_h.Presenter.IsShowing);

        _h.RunFor(TimeSpan.FromSeconds(1));
        Assert.Equal(TimerState.Ringing, Timer(tea).State);
        Assert.Equal(["Ring: Tea", "Hide", "Ring: Tea"], _h.Presenter.Calls);
    }

    [Fact]
    public void Snooze_can_be_repeated()
    {
        var tea = _h.Tracker.StartTimer(TimeSpan.FromMinutes(1), "Tea");
        _h.RunFor(TimeSpan.FromMinutes(1));

        _h.Tracker.Snooze(tea);
        _h.RunFor(TimeSpan.FromMinutes(5));
        _h.Tracker.Snooze(tea);
        _h.RunFor(TimeSpan.FromMinutes(5));

        Assert.Equal(TimerState.Ringing, Timer(tea).State);
        Assert.Equal(["Ring: Tea", "Hide", "Ring: Tea", "Hide", "Ring: Tea"], _h.Presenter.Calls);
    }

    [Fact]
    public void Snoozing_dismissing_or_starting_another_from_a_Timer_that_is_not_ringing_does_nothing()
    {
        var tea = _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Tea");

        _h.Tracker.Snooze(tea);
        _h.Tracker.Dismiss(tea);

        Assert.Equal(TimerState.Running, Timer(tea).State);
        Assert.Null(_h.Tracker.StartAnother(tea));
        Assert.Single(_h.Tracker.Timers);
    }

    [Fact]
    public void Start_another_dismisses_the_Timer_and_starts_one_with_the_same_label_and_duration()
    {
        var tea = _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Tea");
        _h.RunFor(TimeSpan.FromMinutes(5));

        var again = _h.Tracker.StartAnother(tea);

        var timer = Assert.Single(_h.Tracker.Timers);
        Assert.Equal(again, timer.Id);
        Assert.NotEqual(tea, timer.Id);
        Assert.Equal("Tea", timer.Label);
        Assert.Equal(TimeSpan.FromMinutes(5), timer.Duration);
        Assert.Equal(TimerState.Running, timer.State);
        Assert.Equal(TimeSpan.FromMinutes(5), timer.Remaining);
        Assert.Equal(["Ring: Tea", "Hide"], _h.Presenter.Calls);
    }

    [Fact]
    public void Cancelling_all_Timers_ends_every_one_and_hides_the_Interruption()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Tea");
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(25), "Focus");
        _h.RunFor(TimeSpan.FromMinutes(5));

        _h.Tracker.CancelAllTimers();
        _h.RunFor(TimeSpan.FromMinutes(30));

        Assert.Empty(_h.Tracker.Timers);
        Assert.Equal(["Ring: Tea", "Hide"], _h.Presenter.Calls);
    }

    [Fact]
    public void Cancelling_a_Snoozed_Timer_stops_it_ringing_again()
    {
        var tea = _h.Tracker.StartTimer(TimeSpan.FromMinutes(1), "Tea");
        _h.RunFor(TimeSpan.FromMinutes(1));
        _h.Tracker.Snooze(tea);

        _h.Tracker.CancelTimer(tea);
        _h.RunFor(TimeSpan.FromMinutes(10));

        Assert.Empty(_h.Tracker.Timers);
        Assert.Equal(["Ring: Tea", "Hide"], _h.Presenter.Calls);
    }

    [Fact]
    public void Timers_are_listed_ringing_first_then_by_when_they_ring()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(30), "Later");
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(10), "Sooner");
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(1), "Now");
        _h.RunFor(TimeSpan.FromMinutes(1));

        Assert.Equal(["Now", "Sooner", "Later"], _h.Tracker.Timers.Select(t => t.Name));
    }

    [Fact]
    public void Timers_run_through_Idle_and_a_locked_session()
    {
        var focus = _h.Tracker.StartTimer(TimeSpan.FromMinutes(20));
        _h.Signals.StopInput();
        _h.RunFor(TimeSpan.FromMinutes(10));
        _h.Signals.Lock();
        _h.RunFor(TimeSpan.FromMinutes(10));

        Assert.Equal(TimerState.Ringing, Timer(focus).State);
        Assert.Equal(["Ring: 20-minute Timer"], _h.Presenter.Calls);
    }

    [Fact]
    public void A_Timer_that_runs_out_while_the_machine_sleeps_rings_on_waking()
    {
        var tea = _h.Tracker.StartTimer(TimeSpan.FromMinutes(5));
        _h.SleepFor(TimeSpan.FromHours(1));
        _h.RunFor(TimeSpan.Zero);

        Assert.Equal(TimerState.Ringing, Timer(tea).State);
    }

    [Fact]
    public void Timers_do_not_survive_a_restart()
    {
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(5), "Tea");
        _h.Tracker.StartTimer(TimeSpan.FromMinutes(25), "Focus");
        _h.RunFor(TimeSpan.FromMinutes(1));

        _h.Restart();
        _h.RunFor(TimeSpan.FromMinutes(30));

        Assert.Empty(_h.Tracker.Timers);
        Assert.Empty(_h.Presenter.Calls);
    }

    [Fact]
    public void A_Timer_must_last_some_time()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _h.Tracker.StartTimer(TimeSpan.Zero));
        Assert.Empty(_h.Tracker.Timers);
    }
}
