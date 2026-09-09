# Manual verification checklist (macOS)

Use the `ClaudeToolbar-mac` artifact from the newest `mac` workflow run — the run for the latest commit that touched `mac/`. GitHub wraps every artifact in a second zip: unzip the download, then unzip the `ClaudeToolbar-mac.zip` inside it to get `ClaudeToolbar.app`. Run `xattr -dr com.apple.quarantine ClaudeToolbar.app`, move it to `/Applications`, then open it. Sign in to Claude Code first (`claude` in a terminal). Fill in the Result column and send the file back together with `~/Library/Logs/ClaudeToolbar/app.log`.

| # | Check | Expected | Result |
|---|-------|----------|--------|
| 1 | Launch | Item appears in the menu bar within 3 s; no Dock icon; no Keychain prompt | |
| 2 | Real data | Two rows with your real percentages; hover tooltip shows the same plus an "Updated" line ("Updated just now" during the first minute) | |
| 3 | Countdown | Open the popover and watch for 2 minutes: the "resets in" minutes tick down without clicking | |
| 4 | Popover | Left click opens it; clicking elsewhere closes it; clicking the item again closes it | |
| 5 | Menu | Right click (or Control-click) shows Refresh now / Settings… / Launch at login (checked) / Quit; Escape closes it | |
| 6 | Refresh now | Click it in the popover: "Updated 0s ago" appears within 3 s | |
| 7 | Settings live | Open Settings; change preset to Claude: both preview strips and the menu bar item turn orange at once | |
| 8 | Settings rows | Toggle "7-day Opus" on and "Label" off: the item updates at once | |
| 9 | Persistence | Set bar width 60, quit from the menu, relaunch: width still 60 and Settings shows 60 pt | |
| 10 | Second launch | Open the app again while running: only one item; the Settings window opens | |
| 11 | Dark and light | Switch System Settings → Appearance: item text stays readable in both | |
| 12 | Launch at login | Leave it on, log out and back in: item is present. Make sure the app is in `/Applications` before testing this; macOS registers the path it was launched from | |
| 13 | Sleep / wake | Sleep the Mac for 5 minutes, wake it: "Updated" resets within 10 s of waking | |
| 14 | Wi-Fi off | Turn Wi-Fi off for 2 minutes: numbers stay, a small dot appears after them; turn it on: dot disappears within a minute | |
| 15 | Expired token (optional; you will need to sign in to Claude Code again afterwards) | Quit the app. In Terminal: `security delete-generic-password -s "Claude Code-credentials"`, then `mkdir -p /tmp/cfg && echo '{"claudeAiOauth":{"accessToken":"x","expiresAt":1}}' > /tmp/cfg/.credentials.json`, then `CLAUDE_CONFIG_DIR=/tmp/cfg /Applications/ClaudeToolbar.app/Contents/MacOS/ClaudeToolbar &` (`open -a` cannot pass environment variables, so the binary is started directly). Rows dim and show `↻ run claude`; Settings → Account shows Expired. Run `claude` and sign in again: the item recovers within 30 s | |
| 16 | No credentials (optional; same caveat as 15) | Quit the app first. With the Keychain item deleted and `/tmp/cfg/.credentials.json` removed, launch with `CLAUDE_CONFIG_DIR=/tmp/cfg /Applications/ClaudeToolbar.app/Contents/MacOS/ClaudeToolbar &` (`open -a` cannot pass environment variables, so the binary is started directly): item shows `Sign in with claude`; popover shows "Not signed in — run claude". Run `claude` and sign in again | |
| 17 | Notch | On a notched MacBook, open many menu bar apps: the item either stays visible or is hidden by macOS; it never overlaps the notch | |
| 18 | Quit | Quit from the menu: item disappears, process gone (Activity Monitor) | |
| 19 | Logs | `~/Library/Logs/ClaudeToolbar/app.log` has no ERROR lines and contains no token (`grep -c sk-ant` prints 0) | |
| 20 | Refresh interval | Settings → set "Refresh every" to 30 s: Account "Last update" resets at once, then again about every 30 s | |
| 21 | Launch-at-login sync | Toggle it in the popover; the right-click menu checkmark and the Settings toggle match; toggle it in Settings and re-check the menu; System Settings › General › Login Items agrees | |
| 22 | Settings window fits | Open Settings on your smallest display: every section from Preview down to Reset to defaults is reachable (scroll if needed), nothing clipped | |
| 23 | Settings from the popover | Click "Settings…" in the popover: the popover closes and the window comes to the front, not behind other apps | |
| 24 | Reset to defaults | Click it: preview, menu bar item and interval return to defaults at once; relaunch: still default | |
| 25 | Item while highlighted | With the popover open, and again with the right-click menu open, the menu bar item's text stays readable against the highlight, in light and dark mode | |
| 26 | Settings file | `cat ~/Library/Application\ Support/ClaudeToolbar/settings.json` after changing a few settings: valid JSON, values match the UI, no token inside | |
| 27 | Hover wave | Hover the menu bar item: Clawd, the orange pixel character left of the rows, raises his right arm and waves for about a second, then rests; Settings → Behaviour → Mascot = "Waves on hover only" still waves on hover but not at thresholds | |
| 28 | Greeting and threshold wave | Launch the app: one wave shortly after the first successful fetch; when a row first crosses the warning (default 70 %) or critical (default 90 %) threshold in its current period he waves once (verify with Settings → Appearance thresholds set below the current usage, then restore them) | |
| 29 | Mascot off and reduce motion | Mascot = "Off" hides Clawd and the rows shift left; with System Settings → Accessibility → Display → Reduce motion on he never animates | |
| 30 | Hooks install | Settings → Notifications → Install hooks: status says installed; `~/.claude/settings.json` gains six `http` hooks pointing at `http://127.0.0.1:47831/hook`; `settings.json.claudetoolbar-bak` exists next to it | |
| 31 | Needs-you cue | In a terminal run `claude` in a project and ask it to run a command that needs permission: within a second Clawd's badge blinks amber, he waves, a two-note chime plays, and clicking the item away from Clawd opens the popover with the session line "project · needs you · <host> · now" | |
| 32 | Finished cue | Answer the prompt and let Claude finish: badge turns green with a single soft chime; click the item away from Clawd: the popover shows "project · finished · <host> · now"; close the popover and the badge disappears | |
| 33 | Sound off and remove | Settings → untick "Play a chime": repeat 31 with no sound; Remove hooks: status says not installed and the six hooks are gone from settings.json | |
| 34 | Session lines | With two Claude Code sessions running (one in Terminal, one in VS Code) click the item: the popover lists both as `project · state · host · age` with the right host names | |
| 35 | Jump from a line | Click the VS Code line: the popover closes and VS Code comes to the front; click the Terminal line: Terminal comes forward | |
| 36 | Jump from Clawd | Ask one session to run a command that needs permission, put another app in front, then click Clawd himself (the left part of the item): the waiting session's app comes to the front and the badge clears; the item's tooltip ends with "Click Clawd to go to <project> (<host>)" | |
| 37 | Accessibility | Settings → Notifications shows the Accessibility caption and button; grant it, reopen Settings: the caption says it is granted; with two VS Code windows open, jumping raises the one whose title names the project | |
| 38 | Closed window | Quit the terminal app of a session that is still listed and click its line: the popover shows "<project>: window closed" for a few seconds | |
| 39 | Performance | Activity Monitor: idle CPU 0 %, memory unchanged from before this feature; clicks cost no visible CPU | |
