# Manual verification checklist (macOS)

Use the `ClaudeToolbar-mac` artifact from the latest `mac` workflow run. Unzip, run `xattr -dr com.apple.quarantine ClaudeToolbar.app`, then open it. Sign in to Claude Code first (`claude` in a terminal). Fill in the Result column and send the file back together with `~/Library/Logs/ClaudeToolbar/app.log`.

| # | Check | Expected | Result |
|---|-------|----------|--------|
| 1 | Launch | Item appears in the menu bar within 3 s; no Dock icon; no Keychain prompt | |
| 2 | Real data | Two rows with your real percentages; hover tooltip shows the same plus "Updated Ns ago" | |
| 3 | Countdown | Open the popover and watch for 2 minutes: the "resets in" minutes tick down without clicking | |
| 4 | Popover | Left click opens it; clicking elsewhere closes it; clicking the item again closes it | |
| 5 | Menu | Right click (or Control-click) shows Refresh now / Settings… / Launch at login (checked) / Quit; Escape closes it | |
| 6 | Refresh now | Click it in the popover: "Updated 0s ago" appears within 3 s | |
| 7 | Settings live | Open Settings; change preset to Claude: both preview strips and the menu bar item turn orange at once | |
| 8 | Settings rows | Toggle "7-day Opus" on and "Label" off: the item updates at once | |
| 9 | Persistence | Set bar width 60, quit from the menu, relaunch: width still 60 and Settings shows 60 pt | |
| 10 | Second launch | Open the app again while running: only one item; the Settings window opens | |
| 11 | Dark and light | Switch System Settings → Appearance: item text stays readable in both | |
| 12 | Launch at login | Leave it on, log out and back in: item is present | |
| 13 | Sleep / wake | Sleep the Mac for 5 minutes, wake it: "Updated" resets within 10 s of waking | |
| 14 | Wi-Fi off | Turn Wi-Fi off for 2 minutes: numbers stay, a small dot appears after them; turn it on: dot disappears within a minute | |
| 15 | Expired token (optional; you will need to sign in to Claude Code again afterwards) | Quit the app. In Terminal: `security delete-generic-password -s "Claude Code-credentials"`, then `mkdir -p /tmp/cfg && echo '{"claudeAiOauth":{"accessToken":"x","expiresAt":1}}' > /tmp/cfg/.credentials.json`, then `CLAUDE_CONFIG_DIR=/tmp/cfg open -a ClaudeToolbar`. Rows dim and show `↻ run claude`; Settings → Account shows Expired. Run `claude` and sign in again: the item recovers within 30 s | |
| 16 | No credentials (optional; same caveat as 15) | With the Keychain item deleted and `/tmp/cfg/.credentials.json` removed, launch with `CLAUDE_CONFIG_DIR=/tmp/cfg open -a ClaudeToolbar`: item shows `Sign in with claude`; popover shows "Not signed in — run claude". Run `claude` and sign in again | |
| 17 | Notch | On a notched MacBook, open many menu bar apps: the item either stays visible or is hidden by macOS; it never overlaps the notch | |
| 18 | Quit | Quit from the menu: item disappears, process gone (Activity Monitor) | |
| 19 | Logs | `~/Library/Logs/ClaudeToolbar/app.log` has no ERROR lines and contains no token (`grep -c sk-ant` prints 0) | |
