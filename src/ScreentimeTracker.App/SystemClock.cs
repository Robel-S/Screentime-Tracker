using ScreentimeTracker.Core;

namespace ScreentimeTracker.App;

internal sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}
