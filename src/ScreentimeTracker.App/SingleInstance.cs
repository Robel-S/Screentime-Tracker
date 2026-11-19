using System.Threading;

namespace ScreentimeTracker.App;

/// <summary>
/// Keeps one copy of the app running per user session. A later launch asks the running copy to show
/// its main window and then exits. The publish script asks the running copy to quit, so it can flush
/// tracked time before being replaced.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\ScreentimeTracker.Running";

    /// <summary>Set by a later launch: the running copy shows its main window.</summary>
    private const string ShowEventName = @"Local\ScreentimeTracker.Show";

    /// <summary>Set by publish.ps1: the running copy quits as if Quit was picked from the tray.</summary>
    private const string QuitEventName = @"Local\ScreentimeTracker.Quit";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _show;
    private readonly EventWaitHandle _quit;
    private readonly List<RegisteredWaitHandle> _waits = [];

    private SingleInstance(Mutex mutex, EventWaitHandle show, EventWaitHandle quit)
    {
        _mutex = mutex;
        _show = show;
        _quit = quit;
    }

    /// <summary>
    /// Claims the session for this copy, or, when another copy already runs, asks it to show its main
    /// window and returns null.
    /// </summary>
    public static SingleInstance? Claim()
    {
        // The events exist before the mutex is claimed, so a later launch that finds the mutex
        // always finds the Show event too.
        var show = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var quit = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName);
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew) return new SingleInstance(mutex, show, quit);

        mutex.Dispose();
        show.Set();
        show.Dispose();
        quit.Dispose();
        return null;
    }

    /// <summary>Calls <paramref name="onShow"/> or <paramref name="onQuit"/>, on a pool thread, when another process asks.</summary>
    public void Listen(Action onShow, Action onQuit)
    {
        _waits.Add(ThreadPool.RegisterWaitForSingleObject(_show, (_, _) => onShow(), null, Timeout.Infinite, executeOnlyOnce: false));
        _waits.Add(ThreadPool.RegisterWaitForSingleObject(_quit, (_, _) => onQuit(), null, Timeout.Infinite, executeOnlyOnce: true));
    }

    public void Dispose()
    {
        foreach (var wait in _waits) wait.Unregister(null);
        _show.Dispose();
        _quit.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
