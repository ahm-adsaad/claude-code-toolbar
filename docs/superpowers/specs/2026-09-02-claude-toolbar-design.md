# Claude Toolbar — Design Spec

Date: 2026-09-02
Status: Approved

## 1. Goal

A small, good-looking Windows 11 taskbar widget that shows a Claude subscription's
**session (5-hour) usage** and **weekly (7-day, all models) usage**, each with the
percentage used and the time remaining until reset. It sits in the taskbar immediately
left of the notification area ("show hidden icons" chevron), follows the taskbar across
monitor/DPI changes without going "wonky", refreshes itself smoothly with no manual
refresh, and is customisable (colours, rows, behaviour) through a companion settings
window in the same process.

Windows is the only target for this version. A macOS version is out of scope, but the
platform-free `Core` library is kept free of Windows types so a Mac shell can reuse it
later.

## 2. Non-goals

- Own OAuth login or token refresh. Anthropic's consumer terms (Feb 2026) forbid using
  Claude Pro/Max OAuth in third-party products; refresh tokens also appear to be
  single-use, so refreshing would log Claude Code out. We only read the token Claude
  Code already produced and call the read-only usage endpoint.
- Secondary-monitor taskbars. Primary taskbar only.
- Reserving real taskbar space. The widget is an overlay (like every Win11 tool).
- Extra-usage / credit balances, cost estimates, per-project usage.
- Installer or MSIX packaging. Output is a single self-contained exe.

## 3. Stack and project layout

- .NET 10 SDK (installed via `winget install Microsoft.DotNet.SDK.10`), C# 13, nullable
  and implicit usings on, warnings as errors.
- `ClaudeToolbar.sln`
  - `src/ClaudeToolbar.Core/` — `net10.0`, no Windows dependencies. Credentials reader,
    usage API client, models, countdown/format logic, threshold logic, settings model +
    JSON persistence, refresh scheduler with injectable clock.
  - `src/ClaudeToolbar.App/` — `net10.0-windows`, WPF, `UseWindowsForms=true` (for
    `NotifyIcon` only). Widget window, hover flyout, tray icon + menu, settings window,
    Win32 interop (`Interop/NativeMethods.cs`), taskbar tracker, startup registration,
    single-instance guard.
  - `tests/ClaudeToolbar.Core.Tests/` — xUnit + fixtures.
  - `.github/workflows/build.yml` — on push/PR to `main`: restore, build, test, publish
    `win-x64` single-file self-contained, upload artifact.
- Assembly/product name `ClaudeToolbar`, exe `ClaudeToolbar.exe`. Hand-written P/Invoke
  (no CsWin32), no third-party NuGet packages in App; `System.Text.Json` for JSON.
- Publish: `dotnet publish src/ClaudeToolbar.App -c Release -r win-x64 --self-contained
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`.

## 4. Data: credentials and usage

### 4.1 Credentials

- Path: `%CLAUDE_CONFIG_DIR%\.credentials.json` if the env var is set, else
  `%USERPROFILE%\.claude\.credentials.json`.
- Shape (only fields we read):
  ```json
  { "claudeAiOauth": { "accessToken": "...", "expiresAt": 1756800000000,
                       "subscriptionType": "max", "scopes": ["user:profile", "..."] } }
  ```
  `expiresAt` is epoch milliseconds. Token is treated as expired when
  `now >= expiresAt - 60s`.
- `ICredentialsSource.Read()` returns `CredentialsState`: `Missing`, `Invalid` (unparseable),
  `Expired(expiresAt)`, or `Valid(token, expiresAt, subscriptionType)`. Never logs or
  displays the token. Never writes the file.
- The App watches the file (FileSystemWatcher on the directory, debounced 500 ms) and
  triggers a refresh when it changes.

### 4.2 Usage endpoint

- `GET https://api.anthropic.com/api/oauth/usage`
- Headers: `Authorization: Bearer <token>`, `anthropic-beta: oauth-2025-04-20`,
  `Accept: application/json`, `User-Agent: claude-code/2.0.0` (the ecosystem-standard
  value; without it the endpoint rate-limits aggressively). The UA string is a single
  constant in `UsageClient`.
- Response (fields may be absent or `null`; anything unknown is ignored):
  ```json
  { "five_hour":        { "utilization": 42.0, "resets_at": "2026-09-03T04:00:00+00:00" },
    "seven_day":        { "utilization": 18.0, "resets_at": "2026-09-06T08:00:00+00:00" },
    "seven_day_opus":   { "utilization": 5.0,  "resets_at": "..." },
    "seven_day_sonnet": null }
  ```
  `utilization` is 0–100 (clamped on parse). `resets_at` is ISO 8601 with offset.
- Model: `UsageSnapshot { UsageWindow? FiveHour, SevenDay, SevenDayOpus, SevenDaySonnet;
  DateTimeOffset FetchedAt }`, `UsageWindow { double Utilization; DateTimeOffset ResetsAt }`.
- `IUsageClient.FetchAsync(token, ct)` returns `UsageResult`: `Ok(snapshot)`,
  `Unauthorized` (401/403), `RateLimited(retryAfter?)` (429), `Failed(message)` (other
  HTTP/network errors). 10 s timeout.

### 4.3 Refresh scheduler (Core, testable with a fake clock)

- Normal cadence: every `refreshIntervalSeconds` (default 60, range 30–300).
- Immediate refresh on: app start, credentials file change, settings save, manual
  "Refresh now", resume from sleep, network becoming available, and when any window's
  `ResetsAt` passes (so the bar drops promptly after a reset).
- Backoff: on `Failed`/`RateLimited`, next attempt after 15 s doubling to a cap of 300 s
  (or `Retry-After` if larger); reset to normal cadence after the next success.
- On `Unauthorized` or `Expired`: no polling until the credentials file changes; the last
  snapshot is kept and marked stale.
- Countdown text is recomputed locally every second from `ResetsAt`; the API is never
  hit for that.

### 4.4 Formatting rules (Core)

- Remaining time, `remaining = ResetsAt - now`:
  `>= 1 day` → `"3d 4h"`; `>= 1 hour` → `"2h 13m"`; `>= 1 minute` → `"13m"`;
  `> 0` → `"<1m"`; `<= 0` → `"now"`.
- Percent: integer, rounded half away from zero, `"42%"`.
- Bar level: `Ok` if `utilization < warnThreshold`, `Warn` if `< critThreshold`, else
  `Crit`. Defaults 70 / 90; the settings validator enforces `0 < warn < crit <= 100`.

## 5. Taskbar widget (App)

### 5.1 Window

- WPF `Window`: `WindowStyle=None`, `AllowsTransparency=true`, `Background=Transparent`,
  `ShowInTaskbar=false`, `ShowActivated=false`, `Topmost=true`, `ResizeMode=NoResize`,
  `SizeToContent=WidthAndHeight`.
- Extended styles set after source init: `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`
  (removes it from Alt-Tab and prevents focus stealing). `WM_MOUSEACTIVATE` returns
  `MA_NOACTIVATE`.
- Per-monitor DPI v2 via `app.manifest`. All `SetWindowPos` calls use physical pixels;
  the widget's logical size is converted with the window's current DPI.
- Content: a rounded rectangle (`cornerRadius`, `background` ARGB) containing a
  vertical stack of rows. Each row: label (`5h` / `7d` / `7d Opus` / `7d Sonnet`),
  progress bar (`barWidth` logical px, 4 px tall, track + fill), percent, remaining time.
  Font: Segoe UI Variable Text (fallback Segoe UI), `fontSize` (default 11).
  Height is capped at taskbar height minus 4 px; at default settings the two rows fit
  a 48 px taskbar.

### 5.2 Positioning (`TaskbarTracker`)

- Discover: `FindWindow("Shell_TrayWnd")`; accept only if its owning process is
  `explorer.exe`. `FindWindowEx(tray, "TrayNotifyWnd")` → `GetWindowRect` gives the
  notification area rect. `SHAppBarMessage(ABM_GETTASKBARPOS)` gives the taskbar rect
  and edge; `ABM_GETSTATE` gives auto-hide.
- Target rect: `right = notify.left - trayGapPx`, `top = taskbar.top +
  (taskbar.height - widget.height) / 2`. Applied with
  `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)` only when the target differs from the
  current rect (no needless moves, no flicker).
- Re-evaluate on: `SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE)` scoped to explorer's
  process and filtered to the tray/notify HWNDs (tray growth, auto-hide animation);
  `TaskbarCreated` window message (explorer restart → rediscover handles);
  `WM_DISPLAYCHANGE`, `WM_DPICHANGED`, `WM_SETTINGCHANGE` (work area); and a 1 s
  sanity timer.
- Sanity timer also: re-asserts `HWND_TOPMOST` only if the taskbar is above the widget
  in z-order; hides the widget when `SHQueryUserNotificationState` returns
  `QUNS_BUSY`, `QUNS_RUNNING_D3D_FULL_SCREEN` or `QUNS_PRESENTATION_MODE` (if
  `hideInFullscreen`), or when the taskbar is hidden off-screen (auto-hide); shows it
  again otherwise.
- If the taskbar cannot be found the widget is hidden and discovery retries every 3 s;
  the tray icon keeps working.

### 5.3 Interaction

- Hover (400 ms) → non-focusing `Popup` flyout: one line per window (`Session 42% ·
  resets in 2h 13m · at 04:00`), per-model lines when present, "Updated 12 s ago", and
  the current status (OK / stale / expired). Hides on mouse leave.
- Left click → opens (or focuses) the settings window.
- Right click → context menu: Refresh now, Settings…, Run at startup (checkable), Exit.
- Tray icon (`NotifyIcon`): same menu; double-click opens settings. Icon is a static
  16/32 px asset.

### 5.4 Visual states

| State | Widget |
|---|---|
| Loading (first fetch) | rows with `—` and empty bars |
| OK | normal |
| Stale (network/429 backoff) | normal numbers, small dot at the right edge, tooltip "Last update 3m ago" |
| Expired / unauthorized | numbers dimmed to 50% opacity, `↻ run claude` replaces the time on the first row |
| No credentials | single row: `Sign in with claude` |
| Taskbar not found / fullscreen | hidden |

## 6. Settings window (companion)

- Same process. Opened from widget click, tray menu, or launching the exe a second time
  (single-instance guard: named mutex `ClaudeToolbar.Instance`; a second instance signals
  the named event `ClaudeToolbar.OpenSettings` and exits).
- Standard WPF window (~520×640 logical), follows the system app theme (dark/light from
  `HKCU\...\Themes\Personalize\AppsUseLightTheme`), custom flat styling.
- Layout: live widget preview pinned at the top (renders the same control as the widget
  with sample data), then sections:
  - **Appearance**: preset picker (Dark, Light, Claude, Mono — applying a preset writes
    all colour fields), colour pickers for background, text, bar track, bar OK, bar
    warn, bar critical (each an ARGB hex box + swatch + simple HSV picker; background
    also gets an opacity slider that edits its alpha), font size (9–14), corner radius
    (0–12), warn/crit thresholds.
  - **Rows**: toggles for 5h, 7d, 7d Opus, 7d Sonnet rows; toggles for label, bar,
    percent, time; bar width (30–120).
  - **Behaviour**: refresh interval (30–300 s), gap from tray (0–24 px), hide in
    fullscreen, run at startup.
  - **Account**: credentials path in use, token state (valid until / expired / missing),
    subscription type, last successful update, "Refresh now" button, and the hint
    "Run `claude` in a terminal to refresh your login" when expired/missing.
- Changes apply live to the widget and are saved on every change (debounced 300 ms).
  "Reset to defaults" button.

### 6.1 Settings file

`%APPDATA%\ClaudeToolbar\settings.json`, written atomically (temp + rename):

```json
{
  "version": 1,
  "appearance": {
    "preset": "dark",
    "background": "#CC1E1E1E", "text": "#FFF3F3F3", "barTrack": "#33FFFFFF",
    "barOk": "#FF3FB950", "barWarn": "#FFD29922", "barCrit": "#FFF85149",
    "fontSize": 11, "cornerRadius": 6, "warnThreshold": 70, "critThreshold": 90
  },
  "rows": {
    "showFiveHour": true, "showSevenDay": true,
    "showSevenDayOpus": false, "showSevenDaySonnet": false,
    "showLabel": true, "showBar": true, "showPercent": true, "showTime": true,
    "barWidth": 60
  },
  "behavior": {
    "refreshIntervalSeconds": 60, "trayGapPx": 8,
    "hideInFullscreen": true, "runAtStartup": true
  }
}
```

Unknown fields are ignored; missing fields take defaults; out-of-range values are
clamped; an unreadable file is backed up as `settings.json.bad` and replaced with
defaults. `runAtStartup` is mirrored to
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run\ClaudeToolbar` (value = quoted exe
path) whenever it changes and on every launch (so a moved exe re-registers).

## 7. Error handling summary

- Every background operation is wrapped; failures become a status shown in the flyout
  and Account section, never a crash. Unhandled exceptions are logged to
  `%LOCALAPPDATA%\ClaudeToolbar\logs\app.log` (rolling, 1 MB) and the app keeps running.
- Logs never contain tokens or the credentials file contents.
- Clock skew: remaining time is computed from the machine clock; negative values show
  `now` and trigger a refresh once.

## 8. Testing

- Core (xUnit, run in CI):
  - `UsageResponseParser`: full payload, missing per-model fields, nulls, utilization
    clamping, bad JSON → `Failed`.
  - `CredentialsReader`: fixture files for valid / expired / missing / invalid; env var
    override; token never appears in `ToString`.
  - `Formatting`: remaining-time boundaries (days, hours, minutes, <1m, past), percent
    rounding, bar level thresholds.
  - `Settings`: defaults, round-trip, unknown fields ignored, clamping, bad file →
    backup + defaults, validator rejects `warn >= crit`.
  - `RefreshScheduler` with fake clock: cadence, immediate triggers, backoff doubling and
    cap, stop on unauthorized until credentials change, refresh when `ResetsAt` passes.
- App: no UI unit tests. A manual checklist in `docs/verification.md` run at the end of
  the App tasks and by the final review: position next to the tray on the primary
  monitor; drag a window over it (stays on top); change display scale (DPI) and
  resolution; switch primary monitor / dock-undock; restart explorer.exe; enable taskbar
  auto-hide; run a fullscreen video/game (widget hides, returns); open Start/quick
  settings (widget not stolen focus); change colours in settings (live); expire the
  token scenario simulated with a fixture credentials dir via `CLAUDE_CONFIG_DIR`.

## 9. Repository and workflow

- Public repo `ahm-adsaad/claude-code-toolbar`, default branch `main`.
- Commits use the user's git identity, plain descriptive messages, and **no AI
  attribution of any kind** (no co-author trailers, no "generated with" lines, no
  mention of assistants). Push after every completed task.
- Implementation follows a written plan; each task is implemented by one agent,
  reviewed for spec compliance and code quality by separate agents, then committed and
  pushed.
- `README.md` covers: what it shows, how auth works (reads Claude Code's login, never
  writes), install/run, settings, limitations, build from source.

## 10. Known limitations

- Overlay, not reserved space: with a left-aligned, very full taskbar it can overlap
  the rightmost task button.
- Token expiry: if Claude Code hasn't run for ~8 h the token expires and the widget
  shows the last numbers dimmed until Claude Code runs again.
- Primary taskbar only; Windows 11 only (Windows 10 is untested and unsupported).
