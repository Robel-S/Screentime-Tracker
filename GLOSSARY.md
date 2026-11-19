# Screentime Tracker

A personal Windows app that records where computer time goes, lets the user sort it into productive and entertainment use, and interrupts them when a timer they started runs out.

## Tracking

**Screen Time**:
Time during which a Source is in the foreground, the session is unlocked and awake, and the user is not Idle. Time in the Screentime Tracker itself is never Screen Time.
_Avoid_: Usage, uptime, computer time

**Source**:
The thing Screen Time is attributed to: either an App or a Website.
_Avoid_: Activity, program, item

**App**:
A desktop program that can be in the foreground.
_Avoid_: Program, application, process

**Website**:
A hostname, minus any leading "www.", open in the active tab of a supported browser while that browser is in the foreground. Browser time on a Website is attributed to the Website; browser time with no Website (settings, new tab, private windows) is attributed to the browser App.
_Avoid_: Site, page, URL, tab

**Idle**:
The state of having had no keyboard or mouse input for longer than the idle threshold, unless the foreground window is fullscreen. Idle time is never Screen Time.
_Avoid_: AFK, away, inactive

**Day**:
A calendar day from local midnight to midnight, the unit Screen Time is totalled by.
_Avoid_: Date, session

## Categorising

**Category**:
The label a Source belongs to: Productive, Entertainment, or Uncategorized.
_Avoid_: Tag, group, type

**Uncategorized**:
The Category every newly seen Source starts in until the user sorts it.
_Avoid_: Unknown, other, unsorted

## Timer

**Timer**:
A countdown the user starts by hand. It runs independently of what they are doing.
_Avoid_: Limit, alarm, pomodoro

**Interruption**:
The always-on-top alert shown when a Timer runs out, which stays until the user dismisses or snoozes it.
_Avoid_: Notification, popup, toast, alert

**Snooze**:
Dismissing an Interruption for five minutes, after which it reappears.
_Avoid_: Delay, postpone, extend
