# Browser extension reports over localhost, not native messaging

The Edge/Brave extension sends the active tab's domain to the app over a localhost-only port, not over Chromium native messaging. Native messaging is the "official" channel, but it needs per-browser registry manifests and a browser-launched host process that would itself have to relay data to the already-running tray app. That is two hops and more setup for no benefit in a single-user personal tool. The listener binds to 127.0.0.1 only.
