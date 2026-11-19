using System.Globalization;

namespace ScreentimeTracker.Core;

/// <summary>Where a Timer is: Running → Ringing ⇄ Snoozed, until it is Dismissed or Cancelled and gone.</summary>
public enum TimerState
{
    /// <summary>Counting down.</summary>
    Running,

    /// <summary>Ran out; the Interruption lists it.</summary>
    Ringing,

    /// <summary>Ran out and was Snoozed; it rings again when the Snooze ends.</summary>
    Snoozed,
}

/// <summary>Identifies one Timer for as long as it exists.</summary>
public readonly record struct TimerId(long Value);

/// <summary>One Timer as it is now.</summary>
/// <param name="Label">What the user called the Timer, or null when they gave it no label.</param>
/// <param name="Duration">How long the Timer was started for.</param>
/// <param name="Remaining">
/// How long until the Timer rings: until it runs out while Running, or until its Snooze ends while
/// Snoozed. Null while it is Ringing.
/// </param>
public sealed record TimerSnapshot(TimerId Id, string? Label, TimeSpan Duration, TimerState State, TimeSpan? Remaining)
{
    /// <summary>The Timer's name as the user sees it: its label, or its duration, such as "25-minute Timer".</summary>
    public string Name => Label ?? DefaultName(Duration);

    /// <summary>The name of a Timer without a label, such as "25-minute Timer" or "1-hour Timer".</summary>
    public static string DefaultName(TimeSpan duration) =>
        duration.TotalHours >= 1 && duration.TotalHours == Math.Floor(duration.TotalHours)
            ? $"{duration.TotalHours.ToString("0", CultureInfo.CurrentCulture)}-hour Timer"
            : $"{duration.TotalMinutes.ToString("0.##", CultureInfo.CurrentCulture)}-minute Timer";
}

/// <summary>
/// The Timers the user has started, each with its own state machine. They run on the clock alone,
/// whatever the user is doing, and live in memory only. Together they drive the one Interruption
/// through the presenter, which always lists every Timer that is Ringing.
/// </summary>
internal sealed class TimerBoard(IInterruptionPresenter presenter)
{
    public static readonly TimeSpan SnoozeLength = TimeSpan.FromMinutes(5);

    private readonly List<CountingTimer> _timers = [];
    private long _lastId;

    /// <summary>Whether the Interruption is on screen, as far as the board last told the presenter.</summary>
    private bool _showing;

    /// <summary>Every Timer, Ringing ones first, then the rest by when they next ring.</summary>
    public IReadOnlyList<TimerSnapshot> Snapshot(DateTime now) =>
        Ordered(_timers).Select(t => t.Snapshot(now)).ToList();

    public TimerId Start(TimeSpan duration, string? label, DateTime now)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A Timer must last some time.");
        }
        var id = new TimerId(++_lastId);
        _timers.Add(new CountingTimer(id, string.IsNullOrWhiteSpace(label) ? null : label.Trim(), duration, now));
        return id;
    }

    public void Cancel(TimerId id) => Change(() => _timers.RemoveAll(t => t.Id == id));

    public void CancelAll() => Change(_timers.Clear);

    public void Dismiss(TimerId id) =>
        Change(() => _timers.RemoveAll(t => t.Id == id && t.State == TimerState.Ringing));

    public void Snooze(TimerId id, DateTime now) =>
        Change(() => Find(id, TimerState.Ringing)?.Snooze(now));

    /// <summary>Dismisses a Ringing Timer and starts a new one with the same label and duration.</summary>
    public TimerId? StartAnother(TimerId id, DateTime now)
    {
        if (Find(id, TimerState.Ringing) is not { } ringing) return null;
        var next = Start(ringing.Duration, ringing.Label, now);
        Dismiss(id);
        return next;
    }

    public void Tick(DateTime now)
    {
        var startedRinging = false;
        foreach (var timer in _timers)
        {
            startedRinging |= timer.RingIfDue(now);
        }
        if (startedRinging)
        {
            presenter.Ring(Ringing());
            _showing = true;
        }
    }

    private CountingTimer? Find(TimerId id, TimerState state) =>
        _timers.SingleOrDefault(t => t.Id == id && t.State == state);

    /// <summary>Makes a change that can only take Timers off the Interruption, then brings it up to date.</summary>
    private void Change(Action change)
    {
        var before = Ringing().Select(t => t.Id).ToList();
        change();
        var after = Ringing();
        if (!_showing || before.SequenceEqual(after.Select(t => t.Id))) return;
        if (after.Count == 0)
        {
            presenter.Hide();
            _showing = false;
        }
        else
        {
            presenter.Update(after);
        }
    }

    private List<TimerSnapshot> Ringing() =>
        Ordered(_timers.Where(t => t.State == TimerState.Ringing)).Select(t => t.Snapshot(default)).ToList();

    private static IEnumerable<CountingTimer> Ordered(IEnumerable<CountingTimer> timers) =>
        timers.OrderBy(t => t.State != TimerState.Ringing).ThenBy(t => t.RingsAt).ThenBy(t => t.Id.Value);

    /// <summary>One Timer's state machine.</summary>
    private sealed class CountingTimer(TimerId id, string? label, TimeSpan duration, DateTime startedAt)
    {
        public TimerId Id { get; } = id;
        public string? Label { get; } = label;
        public TimeSpan Duration { get; } = duration;
        public TimerState State { get; private set; } = TimerState.Running;

        /// <summary>When the Timer next rings, or when it last rang while it is Ringing.</summary>
        public DateTime RingsAt { get; private set; } = startedAt + duration;

        /// <summary>Starts the Timer ringing if it is due. True when it did.</summary>
        public bool RingIfDue(DateTime now)
        {
            if (State == TimerState.Ringing || now < RingsAt) return false;
            State = TimerState.Ringing;
            return true;
        }

        public void Snooze(DateTime now)
        {
            State = TimerState.Snoozed;
            RingsAt = now + SnoozeLength;
        }

        public TimerSnapshot Snapshot(DateTime now) =>
            new(Id, Label, Duration, State, State == TimerState.Ringing ? null : RingsAt - now);
    }
}
