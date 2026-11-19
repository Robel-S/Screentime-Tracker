using ScreentimeTracker.Core;

namespace ScreentimeTracker.Core.Tests.Harness;

/// <summary>System signals the test scripts by hand.</summary>
public sealed class FakeSystemSignals : ISystemSignals
{
    public AppIdentity? ForegroundApp { get; private set; }

    public void BringToFront(AppIdentity app) => ForegroundApp = app;

    public void NothingInFront() => ForegroundApp = null;
}
