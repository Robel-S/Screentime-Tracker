using ScreentimeTracker.Core;

namespace ScreentimeTracker.Core.Tests.Harness;

/// <summary>
/// System signals the test scripts by hand. The user gives input continuously until the test says
/// they stop.
/// </summary>
public sealed class FakeSystemSignals(FakeClock clock) : ISystemSignals
{
    private DateTime? _lastInput;

    public AppIdentity? ForegroundApp { get; private set; }
    public bool ForegroundIsFullscreen { get; private set; }
    public bool IsSessionActive { get; private set; } = true;
    public TimeSpan SinceLastInput => _lastInput is null ? TimeSpan.Zero : clock.Now - _lastInput.Value;

    public void BringToFront(AppIdentity app) => ForegroundApp = app;

    public void NothingInFront() => ForegroundApp = null;

    /// <summary>The user stops touching the keyboard and mouse from now on.</summary>
    public void StopInput() => _lastInput = clock.Now;

    /// <summary>The user touches the keyboard or mouse, and keeps doing so.</summary>
    public void ResumeInput() => _lastInput = null;

    public void GoFullscreen() => ForegroundIsFullscreen = true;

    public void LeaveFullscreen() => ForegroundIsFullscreen = false;

    public void Lock() => IsSessionActive = false;

    public void Unlock() => IsSessionActive = true;
}
