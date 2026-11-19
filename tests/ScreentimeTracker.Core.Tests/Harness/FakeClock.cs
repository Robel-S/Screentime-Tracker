using ScreentimeTracker.Core;

namespace ScreentimeTracker.Core.Tests.Harness;

/// <summary>A clock that only moves when the test advances it.</summary>
public sealed class FakeClock(DateTime start) : IClock
{
    public DateTime Now { get; private set; } = start;

    public void Advance(TimeSpan by) => Now += by;
}
