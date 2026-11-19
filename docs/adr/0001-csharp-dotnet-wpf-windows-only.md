# C# / .NET 8 + WPF, Windows only

The app is Windows-only and written in C# on .NET 8 with WPF. Its core needs (the foreground window, idle time, a tray icon, an always-on-top Interruption window, run-at-login) are all Windows-specific, and .NET reaches them directly. Electron and Tauri would have made a future cross-platform port easier, but they cost a heavier runtime or Rust, and need extra native shims for the same Windows calls. Going cross-platform later means rewriting the tracking and Interruption layers.
