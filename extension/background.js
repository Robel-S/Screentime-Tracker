// Reports the active tab's raw URL to the Screentime Tracker app over localhost (ADR-0002).
// It contains no attribution logic: the app decides what the URL means. It only sends "no Website"
// (url: null) when the tab is not an http(s) page or no window of this browser is focused.

// Must match LocalTabListener.DefaultPort in the Core.
const ENDPOINT = "http://127.0.0.1:47613/tab";
const HEARTBEAT = "heartbeat";
// The app treats a report as stale after 75 seconds, so this tolerates one missed heartbeat.
const HEARTBEAT_MINUTES = 0.5;

/** Which supported browser this is: "edge" or "brave". Edge and Brave both run this same extension. */
async function detectBrowser() {
  if (navigator.brave && (await navigator.brave.isBrave())) return "brave";
  const brands = navigator.userAgentData?.brands?.map((b) => b.brand) ?? [];
  if (brands.includes("Brave")) return "brave";
  if (brands.includes("Microsoft Edge")) return "edge";
  return null;
}

const browserName = detectBrowser();

/** The active tab's URL in this browser's focused window, or null when there is no Website. */
async function activeUrl() {
  let win;
  try {
    win = await chrome.windows.getLastFocused({ populate: true });
  } catch {
    return null;
  }
  // Without incognito access, a focused private window shows up as no focused window here.
  if (!win || !win.focused || win.incognito) return null;
  const tab = win.tabs?.find((t) => t.active);
  const url = tab?.url ?? "";
  return /^https?:\/\//i.test(url) ? url : null;
}

// Reports go out one at a time, in event order, so a slow older report never lands after a newer one.
let queue = Promise.resolve();
function report() {
  queue = queue.then(send, send);
}

async function send() {
  const browser = await browserName;
  if (!browser) return;
  try {
    await fetch(ENDPOINT, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ browser, url: await activeUrl() }),
    });
  } catch {
    // The app isn't running. The next event or heartbeat tries again.
  }
}

function ensureHeartbeat() {
  chrome.alarms.get(HEARTBEAT, (alarm) => {
    if (!alarm) chrome.alarms.create(HEARTBEAT, { periodInMinutes: HEARTBEAT_MINUTES });
  });
}

// Every time the service worker starts (install, browser start, re-enable, or waking for an event),
// make sure the heartbeat alarm exists, since alarms aren't guaranteed to survive a restart.
ensureHeartbeat();
chrome.runtime.onInstalled.addListener(() => report());
chrome.runtime.onStartup.addListener(() => report());
chrome.alarms.onAlarm.addListener((alarm) => { if (alarm.name === HEARTBEAT) report(); });
chrome.tabs.onActivated.addListener(() => report());
chrome.tabs.onUpdated.addListener((_tabId, change, tab) => {
  if (tab.active && change.url !== undefined) report();
});
chrome.windows.onFocusChanged.addListener(() => report());
