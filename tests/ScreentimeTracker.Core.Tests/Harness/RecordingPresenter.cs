using ScreentimeTracker.Core;

namespace ScreentimeTracker.Core.Tests.Harness;

/// <summary>An Interruption presenter that records what it was told to do.</summary>
public sealed class RecordingPresenter : IInterruptionPresenter
{
    private readonly List<string> _calls = [];

    /// <summary>Every call so far, in order: "Show" or "Hide".</summary>
    public IReadOnlyList<string> Calls => _calls;

    /// <summary>Whether the Interruption is on screen now.</summary>
    public bool IsShowing { get; private set; }

    public void Show()
    {
        _calls.Add("Show");
        IsShowing = true;
    }

    public void Hide()
    {
        _calls.Add("Hide");
        IsShowing = false;
    }
}
