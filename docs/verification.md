# Manual verification checklist

Run with a Release build: `dotnet publish src/ClaudeToolbar.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish` then start `publish\ClaudeToolbar.exe`.

| # | Check | Expected | Result |
|---|-------|----------|--------|
| 1 | Launch | Widget appears left of the tray chevron within 2 s, vertically centred, two rows | OK — widget visible ~2 s after launch, two rows ("5h ... 74% 7m", "7d ... 35% 6d 3h") left of the chevron at x=3154, vertically centred in the 48 px taskbar |
| 2 | Real data | Rows show real percentages and countdowns; hover flyout shows "Updated Ns ago" and reset clock times | OK — hover flyout showed "Session 74% · resets in 6m · at 04:09", "Weekly 35% · resets in 6d 3h · at 07:59", "Updated 49s ago", "OK" |
| 3 | Countdown | The minutes value ticks without any click (watch for 2 minutes) | OK — two screenshots 65 s apart: 5h row went from 6m to 5m with no interaction |
| 4 | Drag a window over it | Widget stays on top | OK — Notepad window moved to overlap the taskbar/widget area; widget rows remained visible on top of it |
| 5 | Click desktop / open Start | Widget stays visible; Start menu opens normally | OK — simulated Windows key opened Start (verified full-screen screenshot); Escape closed it; widget briefly obscured by the Start surface then self-corrected within ~2 s |
| 6 | Focus | With Notepad active, hover and left-click the widget: settings opens, Notepad was not disturbed before the click | OK — foreground handle stayed on Notepad through the hover, changed to "Claude Toolbar Settings" only after the click |
| 7 | Right-click | Menu with Refresh now / Settings… / Run at startup / Exit; closes when clicking elsewhere | needs human |
| 8 | Tray icon | Right-click shows the same menu; double-click opens settings | needs human |
| 9 | Display scale | Settings > System > Display > Scale 100% → 150% → back: widget stays next to the tray, text crisp, no drift | needs human |
| 10 | Resolution / monitor switch | Change resolution or unplug/plug the external monitor: widget re-anchors within 1 s | needs human |
| 11 | Explorer restart | `Stop-Process -Name explorer -Force`: widget returns once the taskbar is back | OK — explorer auto-restarted within 5 s, ClaudeToolbar process survived, widget reappeared at the same position once the tray settled |
| 12 | Auto-hide | Enable taskbar auto-hide: widget hides with the taskbar and follows it back when revealed | needs human |
| 13 | Fullscreen | Play a fullscreen video / F11 browser: widget hides; leaving fullscreen brings it back | needs human |
| 14 | Tray growth | Start an app that adds a tray icon (or toggle "show hidden icons"): widget shifts left immediately | Partial — chevron flyout opened/closed correctly via mouse_event; no visible shift observed because both the flyout and a test NotifyIcon landed in the hidden-icons overflow rather than the pinned tray on this machine |
| 15 | Settings live | Change preset, colours, font size, rows: widget updates instantly and stays right-anchored | OK — clicking the Claude preset chip updated the settings preview and the live taskbar widget's colours instantly, widget stayed right-anchored; Reset to defaults reverted both |
| 16 | Persistence | Close settings, exit app, relaunch: settings kept | OK — toggled Run at startup off, confirmed `false` in settings.json, exited and relaunched: setting still Off; restored to On afterward |
| 17 | Expired token | `CLAUDE_CONFIG_DIR` pointing at a fixture with `expiresAt: 1`: rows dim and show `↻ run claude`; Account card shows "Expired" | OK — rows dimmed with "— ↻ run claude"/"—" placeholders; log showed `Usage state: Expired`; Account card showed "Expired at 04:00" |
| 18 | No credentials | Delete the fixture file while running: widget shows `Sign in with claude` within 2 s; recreate it: rows return | OK — widget showed "Sign in with claude" ~2 s after deleting the fixture; recreating it brought the rows back (expired-state rows, matching the fixture's content) |
| 19 | Network loss | Disable Wi-Fi for 2 minutes: numbers stay, stale dot appears; re-enable: dot disappears on next fetch | needs human — not run; disabling networking was judged unsafe on this remotely-driven session |
| 20 | Sleep / resume | Sleep the machine, wake it: widget is placed correctly and refreshes within a few seconds | needs human |
| 21 | Second launch | Running the exe again opens settings instead of a second widget | OK — exactly one ClaudeToolbar process remained after a second launch; log showed "Another instance is running; asked it to open settings and exiting" |
| 22 | Startup | Reboot: widget is present after sign-in (Run at startup on) | needs human |
| 23 | Logs | `%LOCALAPPDATA%\ClaudeToolbar\logs\app.log` contains no ERROR lines and no token | OK — 0 ERROR lines and 0 token-like patterns across 124 log lines |
