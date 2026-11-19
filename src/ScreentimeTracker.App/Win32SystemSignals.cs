using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ScreentimeTracker.Core;

namespace ScreentimeTracker.App;

/// <summary>
/// Windows adapter for the system signals port: reads the foreground window's process through
/// Win32 each time it is asked. The host asks about once a second.
/// </summary>
internal sealed partial class Win32SystemSignals : ISystemSignals
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    private readonly ConcurrentDictionary<string, AppIdentity> _appsByPath = new(StringComparer.OrdinalIgnoreCase);

    public AppIdentity? ForegroundApp
    {
        get
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero) return null;

            GetWindowThreadProcessId(window, out var processId);
            if (processId == 0) return null;

            var path = ExecutablePath(processId);
            return path is null ? IdentifyByProcessName(processId) : _appsByPath.GetOrAdd(path, Identify);
        }
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

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageName(IntPtr process, uint flags, [Out] char[] buffer, ref int size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);
}
