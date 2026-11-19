namespace ScreentimeTracker.Core;

/// <summary>Where the Timer is: None → Running → Ringing ⇄ Snoozed, and back to None.</summary>
public enum TimerState
{
    /// <summary>No Timer. (Not "Idle", which means no input.)</summary>
    None,

    /// <summary>Counting down.</summary>
    Running,

    /// <summary>Ran out; the Interruption is showing.</summary>
    Ringing,

    /// <summary>Ran out and was Snoozed; the Interruption comes back when the Snooze ends.</summary>
    Snoozed,
}

/// <summary>
/// The Timer state machine. It runs on the clock alone, whatever the user is doing, and lives in
/// memory only. It drives the Interruption through the presenter.
/// </summary>
internal sealed class TimerStateMachine(IInterruptionPresenter presenter)
{
    public static readonly TimeSpan SnoozeLength = TimeSpan.FromMinutes(5);

    /// <summary>When the Timer next rings: when it runs out, or when its Snooze ends.</summary>
    private DateTime _ringsAt;

    public TimerState State { get; private set; } = TimerState.None;

    public TimeSpan? Remaining(DateTime now) =>
        State == TimerState.Running ? _ringsAt - now : null;

    /// <summary>Starts a Timer, replacing any there is, since only one runs at a time.</summary>
    public void Start(TimeSpan duration, DateTime now)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A Timer must last some time.");
        }
        StopRinging();
        State = TimerState.Running;
        _ringsAt = now + duration;
    }

    public void Cancel()
    {
        StopRinging();
        State = TimerState.None;
    }

    public void Dismiss()
    {
        if (State != TimerState.Ringing) return;
        Cancel();
    }

    public void Snooze(DateTime now)
    {
        if (State != TimerState.Ringing) return;
        StopRinging();
        State = TimerState.Snoozed;
        _ringsAt = now + SnoozeLength;
    }

    public void Tick(DateTime now)
    {
        if (State is TimerState.Running or TimerState.Snoozed && now >= _ringsAt)
        {
            State = TimerState.Ringing;
            presenter.Show();
        }
    }

    private void StopRinging()
    {
        if (State == TimerState.Ringing) presenter.Hide();
    }
}
