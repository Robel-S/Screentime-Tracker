using ScreentimeTracker.Core;

namespace ScreentimeTracker.Core.Tests.Harness;

/// <summary>An Interruption presenter that records what it was told to do.</summary>
public sealed class RecordingPresenter : IInterruptionPresenter
{
    private readonly List<string> _calls = [];

    /// <summary>
    /// Every call so far, in order: "Ring: " or "Update: " followed by the names of the ringing Timers
    /// it was given, or "Hide".
    /// </summary>
    public IReadOnlyList<string> Calls => _calls;

    /// <summary>Whether the Interruption is on screen now.</summary>
    public bool IsShowing { get; private set; }

    /// <summary>The names of the Timers the Interruption lists now; empty when it is hidden.</summary>
    public IReadOnlyList<string> Listed { get; private set; } = [];

    public void Ring(IReadOnlyList<TimerSnapshot> ringing) => Record("Ring", ringing);

    public void Update(IReadOnlyList<TimerSnapshot> ringing) => Record("Update", ringing);

    public void Hide()
    {
        _calls.Add("Hide");
        IsShowing = false;
        Listed = [];
    }

    private void Record(string call, IReadOnlyList<TimerSnapshot> ringing)
    {
        Listed = ringing.Select(t => t.Name).ToList();
        _calls.Add($"{call}: {string.Join(", ", Listed)}");
        IsShowing = true;
    }
}
