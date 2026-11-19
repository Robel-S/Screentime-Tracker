# Screentime Tracker browser extension

One Manifest V3 extension for both Edge and Brave. It reports the active tab's URL to the Screentime Tracker app at `http://127.0.0.1:47613/tab` when you switch tabs, when the URL changes, when window focus changes, and every 30 seconds as a heartbeat. The app turns the URL into a Website. Nothing is sent anywhere else.

## Load it (unpacked)

1. Open `edge://extensions` (Edge) or `brave://extensions` (Brave).
2. Turn on **Developer mode**.
3. Click **Load unpacked** and pick this `extension` folder.

Do this once in each browser. Leave "Allow in InPrivate/Private" off: private windows then report no Website, and their time goes to the browser App.

## Check it works

With the Screentime Tracker running, open a Website in the browser for a minute, then open the main window from the tray. The Website should be listed by its hostname, for example `youtube.com`. Settings pages and new tabs count toward the browser App. If you disable the extension, browser time goes back to the browser App within about 75 seconds.
