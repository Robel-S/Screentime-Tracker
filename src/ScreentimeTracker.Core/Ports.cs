namespace ScreentimeTracker.Core;

/// <summary>A desktop program in the foreground, as reported by the operating system.</summary>
/// <param name="Executable">The program's executable file name, for example "notepad.exe". This identifies the App.</param>
/// <param name="DisplayName">A readable name to show for the App, for example "Notepad".</param>
public sealed record AppIdentity(string Executable, string DisplayName);

/// <summary>Port: what the operating system currently says about the user's session.</summary>
public interface ISystemSignals
{
    /// <summary>The App in the foreground, or null when there is none.</summary>
    AppIdentity? ForegroundApp { get; }
}

/// <summary>Port: the current local time.</summary>
public interface IClock
{
    DateTime Now { get; }
}
