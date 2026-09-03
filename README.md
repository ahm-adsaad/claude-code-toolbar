# Claude Toolbar

A small Windows 11 taskbar widget that shows your Claude subscription usage: the 5-hour session window and the 7-day weekly window, each with the percentage used and the time until it resets. It sits in the taskbar just left of the notification area, follows the taskbar across monitors and display scaling, refreshes itself, and is customisable from a built-in settings window.

## What it shows

```
5h ▮▮▮▮▮▯▯▯▯▯ 42%  2h 13m
7d ▮▮▯▯▯▯▯▯▯▯ 18%  3d 4h
```

- Bars change colour from OK to warning to critical at thresholds you choose.
- Hover for exact reset times and the last update time. Left-click opens settings. Right-click for refresh / settings / run at startup / exit.
- Optional rows for the per-model weekly limits (Opus, Sonnet) on plans that report them.

## How it signs in

Claude Toolbar does not have its own login. It reads the credentials that Claude Code stores at `%USERPROFILE%\.claude\.credentials.json` (or `%CLAUDE_CONFIG_DIR%`) and calls Anthropic's read-only usage endpoint with that token. It never writes that file and never refreshes the token. If you have not run Claude Code for about eight hours the token expires; the widget dims and shows `↻ run claude` until you run `claude` again.

## Install and run

1. Download `ClaudeToolbar.exe` from the latest build artifact (Actions → build → ClaudeToolbar-win-x64) or build it yourself (below). The exe is not code-signed, so Windows SmartScreen may show "Windows protected your PC" the first time; choose "More info" then "Run anyway". The single-file build is around 70 MB.
2. Run it. The widget appears in the taskbar and an icon appears in the tray. "Run at startup" is on by default; turn it off from the menu or settings.
3. Make sure you have signed in to Claude Code at least once on this machine (`claude` in a terminal).

## Settings

Open from the widget, the tray icon, or by launching the exe a second time.

- Appearance: presets (Dark, Light, Claude, Mono), colours for background, text, bar track and the three bar levels, font size, corner radius, warning/critical thresholds.
- Rows: which windows to show, and whether to show the label, bar, percent and time.
- Behaviour: refresh interval (30–300 s), gap from the tray, hide when a fullscreen app is active, run at startup.
- Account: which credentials file is in use and the login state.

Settings live in `%APPDATA%\ClaudeToolbar\settings.json`. Logs live in `%LOCALAPPDATA%\ClaudeToolbar\logs\app.log`. If something looks wrong, the log file is the first place to look; run `ClaudeToolbar.exe --dump-taskbar` to write the taskbar rectangles it detected to `%LOCALAPPDATA%\ClaudeToolbar\logs\taskbar-dump.txt`.

## Build from source

Requires the .NET 10 SDK.

```
dotnet test
dotnet publish src/ClaudeToolbar.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

`publish\ClaudeToolbar.exe` is a single self-contained executable.

## Limitations

- The widget is an overlay, not a reserved taskbar region. With a left-aligned, very full taskbar it can overlap the rightmost task button.
- Primary taskbar only; secondary-monitor taskbars are not supported.
- Windows 11 only.

## Project layout

- `src/ClaudeToolbar.Core` — platform-free logic: credentials reading, usage API client, refresh scheduling, formatting, settings, widget model. Fully unit-tested.
- `src/ClaudeToolbar.App` — WPF shell: the taskbar widget, tray icon, settings window and Win32 interop.
- `tests/ClaudeToolbar.Core.Tests` — xUnit tests for Core.
