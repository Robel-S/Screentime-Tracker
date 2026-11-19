using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ScreentimeTracker.Core;

namespace ScreentimeTracker.App;

/// <summary>
/// Windows adapter for the system signals port. The foreground window, fullscreen state and last
/// input are read through Win32 each time they are asked for (the host asks about once a second).
/// Session lock and power events are tracked as they arrive.
/// </summary>
internal sealed partial class Win32SystemSignals : ISystemSignals, IDisposable
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint MonitorDefaultToNearest = 2;
    private const int StyleIndex = -16; // GWL_STYLE
    private const long CaptionStyle = 0x00C00000; // WS_CAPTION

    private readonly ConcurrentDictionary<string, AppIdentity> _appsByPath = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _locked;
    private volatile bool _suspended;
    private long _suspendedAt;

    public Win32SystemSignals()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public AppIdentity? ForegroundApp
    {
        get
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero) return null;

            GetWindowThreadProcessId(window, out var processId);
            if (processId == 0) return null;

            var path = ExecutablePath(processId);
            var app = path is null ? IdentifyByProcessName(processId) : _appsByPath.GetOrAdd(path, Identify);
            return app is not null && processId == Environment.ProcessId ? app with { IsTracker = true } : app;
        }
    }

    public bool ForegroundIsFullscreen
    {
        get
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero || IsDesktop(window)) return false;

            // A maximized window has a title bar and overhangs the screen edges by its border, so it
            // isn't fullscreen. A fullscreen window has no title bar and covers its screen exactly.
            if ((GetWindowLongPtr(window, StyleIndex) & CaptionStyle) == CaptionStyle) return false;
            if (!GetWindowRect(window, out var bounds)) return false;

            var monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
            var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return false;

            var screen = info.Monitor;
            return bounds.Left == screen.Left && bounds.Top == screen.Top
                && bounds.Right == screen.Right && bounds.Bottom == screen.Bottom;
        }
    }

    public TimeSpan SinceLastInput
    {
        get
        {
            var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
            if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
            // Both are milliseconds since boot as 32-bit counters; unsigned subtraction survives wraparound.
            return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time));
        }
    }

    public bool IsSessionActive
    {
        get
        {
            // A Resume event can go missing (Modern Standby is unreliable about it). Input after the
            // Suspend proves the machine is awake again, so tracking never stays stuck off.
            if (_suspended && SinceLastInput < Stopwatch.GetElapsedTime(_suspendedAt)) _suspended = false;
            return !_locked && !_suspended;
        }
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.SessionLogoff:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
                _locked = true;
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.SessionLogon:
                _locked = false;
                break;
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            _suspendedAt = Stopwatch.GetTimestamp();
            _suspended = true;
        }
        else if (e.Mode == PowerModes.Resume)
        {
            _suspended = false;
        }
    }

    /// <summary>The desktop behind all windows covers the screen but is not a fullscreen App.</summary>
    private static bool IsDesktop(IntPtr window)
    {
        if (window == GetShellWindow()) return true;
        var buffer = new char[64];
        var length = GetClassName(window, buffer, buffer.Length);
        var className = new string(buffer, 0, Math.Max(length, 0));
        return className is "Progman" or "WorkerW";
    }

    /// <summary>The App is its executable file name; its display name comes from the file's description.</summary>
    private static AppIdentity Identify(string path)
    {
        string? description = null;
        try
        {
            description = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
        }
        catch (Exception) { }

        return new AppIdentity(
            Path.GetFileName(path),
            string.IsNullOrEmpty(description) ? Path.GetFileNameWithoutExtension(path) : description);
    }

    /// <summary>
    /// Fallback for processes this user can't open, such as elevated ones. Their name is still
    /// visible, so their time isn't dropped.
    /// </summary>
    private static AppIdentity? IdentifyByProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return new AppIdentity(process.ProcessName + ".exe", process.ProcessName);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ExecutablePath(uint processId)
    {
        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero) return null;
        try
        {
            var buffer = new char[1024];
            var length = buffer.Length;
            return QueryFullProcessImageName(process, 0, buffer, ref length)
                ? new string(buffer, 0, length)
                : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetShellWindow();

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetClassName(IntPtr window, [Out] char[] buffer, int size);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr window, out Rect rect);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial long GetWindowLongPtr(IntPtr window, int index);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LastInputInfo info);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageName(IntPtr process, uint flags, [Out] char[] buffer, ref int size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);
}
