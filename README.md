# Screentime Tracker

A personal Windows app that records where your computer time goes, lets you sort it into productive and entertainment use, and interrupts you when a timer you started runs out.

It runs quietly in the system tray, keeps everything on your own machine, and has a Windows 11 style window built with [WPF-UI](https://github.com/lepoco/wpfui) that follows your light or dark setting.

## Features

- **Tracks Screen Time per App and Website.** Time counts while a program is in the foreground and you are at the computer. It stops when you go idle (2 minutes without keyboard or mouse input by default, but not while a fullscreen window is open), lock the screen, or the PC sleeps.
- **Websites, not just "the browser".** With the optional browser extension for Edge or Brave, browser time is split by website, such as `youtube.com` or `github.com`.
- **Categories.** Every App and Website starts Uncategorized. Mark each one Productive or Entertainment from the Source list, and the label applies to all its time, past and future.
- **Overview.** Today as a donut chart split by Category, or the Last 7 Days as a stacked column per day, with totals and the list of Apps and Websites, most-used first.
- **Timers.** Start as many countdowns as you like, each with an optional label, from the window or the tray menu. When one runs out, an always-on-top alert appears with Snooze (5 minutes) and Dismiss buttons.
- **Starts with Windows.** The installed copy can start in the tray when you sign in.

## Requirements

- Windows 10 or 11
- The [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), to build it. The installed app needs no separate .NET install.

## Install

From the repository root, in PowerShell:

```powershell
.\publish.ps1
```

This builds a single self-contained `ScreentimeTracker.exe`, installs it to `%LOCALAPPDATA%\Programs\ScreentimeTracker`, and starts it. Run the script again to update; it asks a running copy to quit cleanly first, so no tracked time is lost. Pass `-NoLaunch` to install without starting it.

The app starts in the tray. Click the tray icon to open the window. Closing the window keeps it running; choose **Quit** from the tray menu to stop it.

### Browser extension (optional)

To see time per Website, load the extension in [`extension/`](extension/) into Edge or Brave. The steps are in [extension/README.md](extension/README.md). Without it, browser time is counted as time in the browser App.

## Your data

Everything stays on your computer. Tracked time is stored in a SQLite database at `%LOCALAPPDATA%\ScreentimeTracker\screentime.db`. The browser extension talks only to the app, over `127.0.0.1`. Nothing is sent anywhere else.

## Development

```powershell
dotnet build
dotnet test
```

| Path | What's there |
| --- | --- |
| `src/ScreentimeTracker.Core` | Tracking, Categories, Timers and storage, with no UI. |
| `src/ScreentimeTracker.App` | The WPF tray app, its window, charts and Win32 integration. |
| `tests/ScreentimeTracker.Core.Tests` | Tests for the Core. |
| `extension/` | The Manifest V3 browser extension. |
| `docs/adr/` | Architecture decision records. |

[GLOSSARY.md](GLOSSARY.md) defines the terms used throughout the code, such as Screen Time, Source, Idle and Interruption.

A development build tracks into the same database as the installed copy, and only one copy runs at a time. Quit the installed copy from the tray before running a development build.

## License

[MIT](LICENSE)
