# Claude Toolbar

[![windows](https://github.com/ahm-adsaad/claude-code-toolbar/actions/workflows/build.yml/badge.svg)](https://github.com/ahm-adsaad/claude-code-toolbar/actions/workflows/build.yml) [![mac](https://github.com/ahm-adsaad/claude-code-toolbar/actions/workflows/mac.yml/badge.svg)](https://github.com/ahm-adsaad/claude-code-toolbar/actions/workflows/mac.yml)

Your Claude subscription usage, always in view: the 5-hour session window and the 7-day weekly window, each with the percentage used and the time until it resets. On Windows 11 it is a taskbar widget that sits just left of the notification area; on macOS it is a menu bar item with a popover. Both refresh themselves, colour the bars at thresholds you choose, and are customisable from a built-in settings window.

| Platform | Requires | Download | Manual checklist |
|---|---|---|---|
| Windows 11 | nothing else (self-contained exe) | Actions → windows → `ClaudeToolbar-win-x64` | [docs/verification.md](docs/verification.md) |
| macOS 14 or newer, Apple Silicon or Intel | nothing else | Actions → mac → `ClaudeToolbar-mac` | [docs/verification-mac.md](docs/verification-mac.md) |

Both apps are version 0.1.0 and read the same data the same way.

## How it works on both platforms

Claude Toolbar has no login of its own. It reads the credentials Claude Code saved when you signed in and calls Anthropic's read-only usage endpoint with that token, once a minute by default. It never writes the credentials, never refreshes the token, and never sends it anywhere but that one endpoint. If you have not run Claude Code for about eight hours the token expires; the display dims and shows `↻ run claude` until you run `claude` again.

The rows, thresholds and refresh interval are the same on both platforms:

- Rows: the 5-hour session window and the 7-day weekly window, plus optional rows for the per-model weekly limits (Opus, Sonnet) on plans that report them.
- Bars change colour from OK to warning to critical at thresholds you choose (defaults 70 % and 90 %).
- Refresh every 30 to 300 seconds (default 60), with an immediate refresh after sleep, when the network comes back, and when a window resets.

Neither build is code-signed, so each OS shows a one-time warning on first launch; the steps to get past it are under each platform below.

## Windows

### What it shows

```
5h ▮▮▮▮▮▯▯▯▯▯ 42%  2h 13m
7d ▮▮▯▯▯▯▯▯▯▯ 18%  3d 4h
```

Hover for exact reset times and the last update time. Left-click opens settings. Right-click for refresh / settings / run at startup / exit. A tray icon offers the same menu.

### How it signs in

It reads `%USERPROFILE%\.claude\.credentials.json` (or `%CLAUDE_CONFIG_DIR%\.credentials.json` when that variable is set) and watches the file, so signing in with `claude` is picked up within a couple of seconds.

### Install and run

1. Download `ClaudeToolbar.exe` from the latest `windows` workflow run (Actions → windows → the newest run → Artifacts → ClaudeToolbar-win-x64) or build it yourself (below). Windows SmartScreen may show "Windows protected your PC" the first time; choose "More info" then "Run anyway". The single-file build is around 70 MB.
2. Run it. The widget appears in the taskbar and an icon appears in the tray. "Run at startup" is on by default; turn it off from the menu or settings.
3. Make sure you have signed in to Claude Code at least once on this machine (`claude` in a terminal).

### Settings

Open from the widget, the tray icon, or by launching the exe a second time.

- Appearance: presets (Dark, Light, Claude, Mono), colours for background, text, bar track and the three bar levels, font size, corner radius, warning/critical thresholds.
- Rows: which windows to show, and whether to show the label, bar, percent and time.
- Behaviour: refresh interval, gap from the tray, hide when a fullscreen app is active, run at startup.
- Account: which credentials file is in use and the login state.

Settings live in `%APPDATA%\ClaudeToolbar\settings.json`. Logs live in `%LOCALAPPDATA%\ClaudeToolbar\logs\app.log`. If something looks wrong, the log file is the first place to look; run `ClaudeToolbar.exe --dump-taskbar` to write the taskbar rectangles it detected to `%LOCALAPPDATA%\ClaudeToolbar\logs\taskbar-dump.txt`.

### Build from source

Requires the .NET 10 SDK.

```
dotnet test
dotnet publish src/ClaudeToolbar.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

`publish\ClaudeToolbar.exe` is a single self-contained executable.

### Limitations

- The widget is an overlay, not a reserved taskbar region. With a left-aligned, very full taskbar it can overlap the rightmost task button.
- Primary taskbar only; secondary-monitor taskbars are not supported.
- Windows 11 only; Windows 10 is untested.

## macOS

### What it shows

```
5h ▬▬▬▬▬▬▬▬ 42%   7d ▬▬▬▬▬▬▬▬ 18%
```

One line in the menu bar with compact bars and percentages (reset times can be added in Settings). Click it for the full detail: each window with its bar, reset countdown and clock time, the last update time and the account state, plus Refresh now, Settings, a Launch at login switch and Quit. Right-click (or Control-click) for the same actions as a menu.

### How it signs in

It reads the login that Claude Code stores in your Keychain (service `Claude Code-credentials`) through the same `security` tool Claude Code uses, so no permission prompt appears. If that item is missing it falls back to `~/.claude/.credentials.json` (or `$CLAUDE_CONFIG_DIR`). There is no file to watch on macOS, so after you sign in with `claude` the item recovers within about 30 seconds, or at once with Refresh now.

If macOS ever shows a Keychain prompt for ClaudeToolbar, choose **Always Allow**.

### Install and run

1. Download `ClaudeToolbar-mac.zip` from the latest `mac` workflow run (Actions → mac → the newest run → Artifacts → ClaudeToolbar-mac). GitHub wraps every artifact in a second zip: unzip the download, then unzip the `ClaudeToolbar-mac.zip` inside it to get `ClaudeToolbar.app`.
2. The app is not notarized, so macOS blocks it on first launch. Remove the quarantine flag once:
   ```
   xattr -dr com.apple.quarantine ClaudeToolbar.app
   ```
   Alternatively open it, dismiss the warning, then go to System Settings → Privacy & Security and click **Open Anyway**.
3. Move `ClaudeToolbar.app` to Applications and open it. The item appears in the menu bar; there is no Dock icon. Opening the app again while it is running brings up Settings.
4. Make sure you have signed in to Claude Code at least once on this Mac (`claude` in a terminal).

### Settings

Open from the popover, the right-click menu, or by opening the app a second time. A live preview of the menu bar item on dark and light backgrounds sits at the top.

- Rows: which windows to show, and whether to show the label, bar, percent and time; bar width.
- Appearance: presets (Default, Claude, Mono), colours for the three bar levels, warning/critical thresholds. The menu bar supplies the background and text colours itself.
- Behaviour: refresh interval, launch at login.
- Account: credentials source (Keychain or file), login state, plan, last update, Refresh now.

Settings live in `~/Library/Application Support/ClaudeToolbar/settings.json`. Logs live in `~/Library/Logs/ClaudeToolbar/app.log`.

### Build from source

Requires Xcode 16 or the Swift 6 toolchain.

```
cd mac
swift test
bash scripts/build-app.sh
```

`build/ClaudeToolbar.app` is the app bundle (universal, ad-hoc signed); `build/ClaudeToolbar-mac.zip` is the same thing zipped. `build/ClaudeToolbar.app/Contents/MacOS/ClaudeToolbar --snapshot out` renders every view with sample data to PNG files in `out`, which is how the UI is reviewed in CI without a Mac. The Core library and its tests also build on Linux and Windows with the Swift toolchain (`swift test` in `mac/`).

### Limitations

- Not notarized: the first launch needs the quarantine step above.
- On a notched MacBook a crowded menu bar can hide the item, as with any menu bar app; narrow it by turning off the label or the bar in Settings.
- If a future Claude Code release binds its Keychain item to its own code signature, the item shows `Sign in with claude` (or macOS shows a Keychain prompt, where Always Allow fixes it).

## Project layout

- `src/ClaudeToolbar.Core` — Windows: platform-free logic (credentials reading, usage API client, refresh scheduling, formatting, settings, widget model). Fully unit-tested.
- `src/ClaudeToolbar.App` — Windows: WPF shell with the taskbar widget, tray icon, settings window and Win32 interop.
- `tests/ClaudeToolbar.Core.Tests` — xUnit tests for the Windows Core.
- `mac/Sources/ClaudeToolbarCore` — macOS: the same logic ported to Swift (Foundation only; builds and tests on macOS, Linux and Windows).
- `mac/Sources/ClaudeToolbar` — macOS: the menu bar app (status item renderer, popover, settings window, Keychain reader, snapshot mode).
- `mac/Tests/ClaudeToolbarCoreTests` — XCTest tests for the Swift Core.
- `mac/scripts` — `build-app.sh` assembles the universal app bundle; `make-icns.sh` builds the icon.
- `.github/workflows` — `build.yml` (the `windows` workflow) and `mac.yml`; each uploads its app as an artifact, and `mac.yml` also uploads the rendered snapshots.
- `tools` — icon generators for both platforms and a Windows taskbar screenshot helper.
- `docs` — the two manual verification checklists.
