# Claude Toolbar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows 11 taskbar widget (plus an in-process settings window) that shows Claude session and weekly usage with reset countdowns, reading Claude Code's local login.

**Architecture:** A platform-free `ClaudeToolbar.Core` library owns all logic (credentials, usage API, scheduling, formatting, settings, widget model, placement math) and is fully unit-tested. A WPF `ClaudeToolbar.App` hosts a non-activating overlay window parked left of the tray, a tray icon, and a settings window; it talks to Core through small interfaces and does all Win32 work in an `Interop` folder.

**Tech Stack:** .NET 10 SDK, C# 13, WPF (+ WinForms only for `NotifyIcon`), xUnit, System.Text.Json, hand-written P/Invoke, GitHub Actions (windows-latest).

**Spec:** `docs/superpowers/specs/2026-09-02-claude-toolbar-design.md`

## Global Constraints

- **No AI attribution anywhere in git.** Commit messages are plain descriptions of the change. Never add `Co-Authored-By` trailers, "Generated with" lines, or any mention of Claude/assistants as authors. Commit with the user's existing git identity (already configured). Push to `origin main` after every task.
- .NET 10 SDK; `global.json` pins `10.0.100` with `rollForward: latestFeature`. Core targets `net10.0`, App targets `net10.0-windows`, x64 only.
- `Directory.Build.props`: `Nullable` and `ImplicitUsings` enabled, `TreatWarningsAsErrors` true, `LangVersion` latest.
- `ClaudeToolbar.Core` must not reference any Windows-only API or package. `ClaudeToolbar.App` uses no third-party NuGet packages. JSON is `System.Text.Json` only.
- The app never writes the credentials file, never calls any OAuth token endpoint, and never logs a token.
- Exe/assembly/product name: `ClaudeToolbar`. Settings file: `%APPDATA%\ClaudeToolbar\settings.json`. Logs: `%LOCALAPPDATA%\ClaudeToolbar\logs\app.log`.
- Usage endpoint `https://api.anthropic.com/api/oauth/usage` with headers `Authorization: Bearer <token>`, `anthropic-beta: oauth-2025-04-20`, `Accept: application/json`, `User-Agent: claude-code/2.0.0`.
- Test commands run from the repo root: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~<Name>"`. Run the whole suite (`dotnet test`) before every commit.
- Shell: commands below are written for Git Bash. PowerShell-only scripts are marked. The running app is started with `Start-Process` and stopped with `Stop-Process -Name ClaudeToolbar` from PowerShell.
- Repo root: `C:\Users\ahmad\Desktop\GitHubRepos\claude-code-toolbar`.

## File Map

```
global.json                                   SDK pin
Directory.Build.props                         shared compiler settings
ClaudeToolbar.sln
.github/workflows/build.yml                   CI: build, test, publish artifact
tools/make-icon.ps1                           generates src/ClaudeToolbar.App/Assets/app.ico
tools/screenshot-taskbar.ps1                  captures the bottom-right of the primary screen to PNG
src/ClaudeToolbar.Core/
  Time/IClock.cs                              IClock, SystemClock
  Formatting/RemainingTimeFormatter.cs        "3d 4h" / "2h 13m" / "13m" / "<1m" / "now"
  Formatting/PercentFormatter.cs              "42%"
  Formatting/BarLevel.cs                      enum Ok/Warn/Crit + BarLevelResolver
  Usage/UsageWindow.cs, UsageSnapshot.cs      models
  Usage/UsageResult.cs                        Ok / Unauthorized / RateLimited / Failed
  Usage/UsageResponseParser.cs                JSON -> UsageResult
  Usage/IUsageClient.cs, UsageClient.cs       HTTP client
  Credentials/CredentialsState.cs             Missing / Invalid / Expired / Valid
  Credentials/ICredentialsSource.cs
  Credentials/CredentialsPaths.cs             path resolution (CLAUDE_CONFIG_DIR)
  Credentials/FileCredentialsSource.cs        reads + parses the file
  Settings/AppSettings.cs                     AppearanceSettings, RowSettings, BehaviorSettings, AppSettings, SettingsJson
  Settings/Presets.cs                         dark / light / claude / mono
  Settings/SettingsValidator.cs               Normalize (clamp, defaults)
  Settings/SettingsStore.cs                   load/save atomic, .bad backup
  Refresh/RefreshScheduler.cs                 cadence + backoff decisions
  Refresh/MonitorState.cs                     UsageStatus enum + MonitorState record
  Refresh/UsageMonitor.cs                     orchestrates credentials -> client -> state
  Layout/RectI.cs, WidgetPlacement.cs         placement math in physical pixels
  Widget/WidgetModel.cs                       WidgetRow, WidgetModel, WidgetModelBuilder
  Widget/FlyoutModel.cs                       FlyoutModel, FlyoutModelBuilder, AgoFormatter
src/ClaudeToolbar.App/
  ClaudeToolbar.App.csproj, app.manifest, App.xaml, App.xaml.cs
  Assets/app.ico
  Services/Log.cs                             rolling file log
  Services/SingleInstance.cs                  mutex + named event
  Services/StartupRegistration.cs             HKCU Run key
  Services/CredentialsWatcher.cs              FileSystemWatcher, debounced
  Services/SystemTheme.cs                     AppsUseLightTheme
  Tray/TrayIcon.cs                            NotifyIcon + menu
  Tray/IconLoader.cs                          loads app.ico from resources
  Interop/NativeMethods.cs                    P/Invoke
  Interop/TaskbarLayout.cs, TaskbarLocator.cs discovery and refresh of tray rects
  Interop/WinEventHook.cs                     EVENT_OBJECT_LOCATIONCHANGE
  Interop/ShellState.cs                       fullscreen query, z-order check
  Interop/TaskbarTracker.cs                   events + 1 s sanity timer -> Changed
  Widget/WidgetTheme.cs                       brushes from AppearanceSettings
  Widget/UsageRowsControl.cs                  renders WidgetModel rows
  Widget/WidgetWindow.xaml(.cs)               overlay window, styles, WndProc, flyout, menu
  Widget/WidgetController.cs                  ties monitor + tracker + window + settings together
  Settings/SettingsViewModel.cs
  Settings/ColorEditor.xaml(.cs)              swatch + hex + HSVA sliders
  Settings/SettingsWindow.xaml(.cs)
  Settings/Styles.xaml                        card/button/textbox styles
tests/ClaudeToolbar.Core.Tests/
  Fakes/FakeClock.cs, FakeUsageClient.cs, FakeCredentialsSource.cs
  <one test file per Core type>
docs/verification.md                          manual checklist
README.md
```

---

### Task 1: Solution scaffold, Core + test projects, CI

**Files:**
- Create: `global.json`, `Directory.Build.props`, `ClaudeToolbar.sln`
- Create: `src/ClaudeToolbar.Core/ClaudeToolbar.Core.csproj`
- Create: `tests/ClaudeToolbar.Core.Tests/ClaudeToolbar.Core.Tests.csproj`, `tests/ClaudeToolbar.Core.Tests/SmokeTests.cs`
- Create: `.github/workflows/build.yml`

**Interfaces:**
- Produces: the solution every later task builds into; test command convention.

- [ ] **Step 1: Confirm the SDK**

Run: `dotnet --list-sdks`
Expected: a `10.0.xxx` line. If none, run in PowerShell: `winget install --id Microsoft.DotNet.SDK.10 --exact --silent --accept-source-agreements --accept-package-agreements` and open a new shell.

- [ ] **Step 2: Write `global.json` and `Directory.Build.props`**

`global.json`:
```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

`Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
    <Product>ClaudeToolbar</Product>
    <Version>0.1.0</Version>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Create the solution and projects**

```bash
cd "C:/Users/ahmad/Desktop/GitHubRepos/claude-code-toolbar"
dotnet new sln -n ClaudeToolbar
dotnet new classlib -n ClaudeToolbar.Core -o src/ClaudeToolbar.Core -f net10.0
rm src/ClaudeToolbar.Core/Class1.cs
mkdir -p tests/ClaudeToolbar.Core.Tests
```

Write `tests/ClaudeToolbar.Core.Tests/ClaudeToolbar.Core.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.1" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\ClaudeToolbar.Core\ClaudeToolbar.Core.csproj" />
  </ItemGroup>
</Project>
```
If `dotnet restore` reports that one of those package versions does not exist, run `dotnet add tests/ClaudeToolbar.Core.Tests package <id>` for that id to take the latest and keep going.

```bash
dotnet sln add src/ClaudeToolbar.Core tests/ClaudeToolbar.Core.Tests
```

- [ ] **Step 4: Write a smoke test**

`tests/ClaudeToolbar.Core.Tests/SmokeTests.cs`:
```csharp
namespace ClaudeToolbar.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreAssemblyLoads()
    {
        var assembly = typeof(SmokeTests).Assembly.GetReferencedAssemblies()
            .Single(a => a.Name == "ClaudeToolbar.Core");
        Assert.NotNull(assembly);
    }
}
```

- [ ] **Step 5: Build and run tests**

Run: `dotnet test`
Expected: 1 passed.

- [ ] **Step 6: Add CI workflow**

`.github/workflows/build.yml`:
```yaml
name: build

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release
```

- [ ] **Step 7: Commit and push**

```bash
git add -A
git commit -m "Scaffold solution with Core library, test project and CI"
git push origin main
```

---

### Task 2: Clock and formatting

**Files:**
- Create: `src/ClaudeToolbar.Core/Time/IClock.cs`
- Create: `src/ClaudeToolbar.Core/Formatting/RemainingTimeFormatter.cs`, `PercentFormatter.cs`, `BarLevel.cs`
- Create: `tests/ClaudeToolbar.Core.Tests/Fakes/FakeClock.cs`
- Test: `tests/ClaudeToolbar.Core.Tests/Formatting/RemainingTimeFormatterTests.cs`, `PercentFormatterTests.cs`, `BarLevelResolverTests.cs`

**Interfaces:**
- Produces: `IClock { DateTimeOffset UtcNow }`, `SystemClock.Instance`, `RemainingTimeFormatter.Format(TimeSpan)` / `Format(DateTimeOffset resetsAt, DateTimeOffset now)`, `PercentFormatter.Format(double)`, `enum BarLevel { Ok, Warn, Crit }`, `BarLevelResolver.Resolve(double utilization, int warn, int crit)`.

- [ ] **Step 1: Write the failing tests**

`tests/ClaudeToolbar.Core.Tests/Fakes/FakeClock.cs`:
```csharp
using ClaudeToolbar.Core.Time;

namespace ClaudeToolbar.Core.Tests.Fakes;

public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset start) => UtcNow = start;
    public DateTimeOffset UtcNow { get; set; }
    public void Advance(TimeSpan by) => UtcNow += by;
}
```

`tests/ClaudeToolbar.Core.Tests/Formatting/RemainingTimeFormatterTests.cs`:
```csharp
using ClaudeToolbar.Core.Formatting;

namespace ClaudeToolbar.Core.Tests.Formatting;

public class RemainingTimeFormatterTests
{
    [Theory]
    [InlineData(3, 4, 10, 0, "3d 4h")]
    [InlineData(1, 0, 0, 0, "1d 0h")]
    [InlineData(0, 23, 59, 59, "23h 59m")]
    [InlineData(0, 2, 13, 5, "2h 13m")]
    [InlineData(0, 1, 0, 0, "1h 0m")]
    [InlineData(0, 0, 59, 59, "59m")]
    [InlineData(0, 0, 13, 59, "13m")]
    [InlineData(0, 0, 1, 0, "1m")]
    [InlineData(0, 0, 0, 30, "<1m")]
    [InlineData(0, 0, 0, 0, "now")]
    public void FormatsBoundaries(int d, int h, int m, int s, string expected)
    {
        var span = new TimeSpan(d, h, m, s);
        Assert.Equal(expected, RemainingTimeFormatter.Format(span));
    }

    [Fact]
    public void NegativeIsNow()
    {
        Assert.Equal("now", RemainingTimeFormatter.Format(TimeSpan.FromMinutes(-5)));
    }

    [Fact]
    public void FormatsFromInstants()
    {
        var now = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var reset = now.AddHours(2).AddMinutes(13);
        Assert.Equal("2h 13m", RemainingTimeFormatter.Format(reset, now));
    }
}
```

`tests/ClaudeToolbar.Core.Tests/Formatting/PercentFormatterTests.cs`:
```csharp
using ClaudeToolbar.Core.Formatting;

namespace ClaudeToolbar.Core.Tests.Formatting;

public class PercentFormatterTests
{
    [Theory]
    [InlineData(42.4, "42%")]
    [InlineData(42.5, "43%")]
    [InlineData(0, "0%")]
    [InlineData(100, "100%")]
    [InlineData(-1, "0%")]
    [InlineData(150, "100%")]
    public void FormatsRoundedAndClamped(double value, string expected)
    {
        Assert.Equal(expected, PercentFormatter.Format(value));
    }
}
```

`tests/ClaudeToolbar.Core.Tests/Formatting/BarLevelResolverTests.cs`:
```csharp
using ClaudeToolbar.Core.Formatting;

namespace ClaudeToolbar.Core.Tests.Formatting;

public class BarLevelResolverTests
{
    [Theory]
    [InlineData(0, BarLevel.Ok)]
    [InlineData(69.9, BarLevel.Ok)]
    [InlineData(70, BarLevel.Warn)]
    [InlineData(89.9, BarLevel.Warn)]
    [InlineData(90, BarLevel.Crit)]
    [InlineData(100, BarLevel.Crit)]
    public void ResolvesAgainstThresholds(double utilization, BarLevel expected)
    {
        Assert.Equal(expected, BarLevelResolver.Resolve(utilization, 70, 90));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~Formatting"`
Expected: build errors (types not defined).

- [ ] **Step 3: Implement**

`src/ClaudeToolbar.Core/Time/IClock.cs`:
```csharp
namespace ClaudeToolbar.Core.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public static SystemClock Instance { get; } = new();
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
```

`src/ClaudeToolbar.Core/Formatting/RemainingTimeFormatter.cs`:
```csharp
namespace ClaudeToolbar.Core.Formatting;

public static class RemainingTimeFormatter
{
    public static string Format(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero) return "now";
        if (remaining < TimeSpan.FromMinutes(1)) return "<1m";
        if (remaining < TimeSpan.FromHours(1)) return $"{(int)remaining.TotalMinutes}m";
        if (remaining < TimeSpan.FromDays(1)) return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
        return $"{(int)remaining.TotalDays}d {remaining.Hours}h";
    }

    public static string Format(DateTimeOffset resetsAt, DateTimeOffset now) => Format(resetsAt - now);
}
```

`src/ClaudeToolbar.Core/Formatting/PercentFormatter.cs`:
```csharp
namespace ClaudeToolbar.Core.Formatting;

public static class PercentFormatter
{
    public static string Format(double utilization)
    {
        var clamped = Math.Clamp(utilization, 0, 100);
        var rounded = (int)Math.Round(clamped, MidpointRounding.AwayFromZero);
        return $"{rounded}%";
    }
}
```

`src/ClaudeToolbar.Core/Formatting/BarLevel.cs`:
```csharp
namespace ClaudeToolbar.Core.Formatting;

public enum BarLevel
{
    Ok,
    Warn,
    Crit,
}

public static class BarLevelResolver
{
    public static BarLevel Resolve(double utilization, int warnThreshold, int critThreshold)
    {
        if (utilization < warnThreshold) return BarLevel.Ok;
        if (utilization < critThreshold) return BarLevel.Warn;
        return BarLevel.Crit;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~Formatting"`
Expected: all pass (19 tests).

- [ ] **Step 5: Commit and push**

```bash
git add -A
git commit -m "Add clock abstraction and usage formatting helpers"
git push origin main
```

---

### Task 3: Usage models and response parser

**Files:**
- Create: `src/ClaudeToolbar.Core/Usage/UsageWindow.cs`, `UsageSnapshot.cs`, `UsageResult.cs`, `UsageResponseParser.cs`
- Test: `tests/ClaudeToolbar.Core.Tests/Usage/UsageResponseParserTests.cs`, `UsageSnapshotTests.cs`

**Interfaces:**
- Produces: `record UsageWindow(double Utilization, DateTimeOffset? ResetsAt)`; `record UsageSnapshot(UsageWindow? FiveHour, UsageWindow? SevenDay, UsageWindow? SevenDayOpus, UsageWindow? SevenDaySonnet, DateTimeOffset FetchedAt)` with `IEnumerable<UsageWindow> Windows` and `DateTimeOffset? NextReset`; abstract record `UsageResult` with nested `Ok(UsageSnapshot Snapshot)`, `Unauthorized`, `RateLimited(TimeSpan? RetryAfter)`, `Failed(string Message)`; `UsageResponseParser.Parse(string json, DateTimeOffset fetchedAt) : UsageResult`.

- [ ] **Step 1: Write the failing tests**

`tests/ClaudeToolbar.Core.Tests/Usage/UsageResponseParserTests.cs`:
```csharp
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Tests.Usage;

public class UsageResponseParserTests
{
    private static readonly DateTimeOffset FetchedAt = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    private const string FullPayload = """
        {
          "five_hour":        { "utilization": 42.0, "resets_at": "2026-09-03T14:00:00+00:00" },
          "seven_day":        { "utilization": 18.0, "resets_at": "2026-09-06T08:00:00+00:00" },
          "seven_day_opus":   { "utilization": 5.0,  "resets_at": "2026-09-06T08:00:00+00:00" },
          "seven_day_sonnet": null,
          "extra_usage":      { "is_enabled": false }
        }
        """;

    [Fact]
    public void ParsesFullPayload()
    {
        var result = Assert.IsType<UsageResult.Ok>(UsageResponseParser.Parse(FullPayload, FetchedAt));
        var s = result.Snapshot;
        Assert.Equal(42.0, s.FiveHour!.Utilization);
        Assert.Equal(new DateTimeOffset(2026, 9, 3, 14, 0, 0, TimeSpan.Zero), s.FiveHour.ResetsAt);
        Assert.Equal(18.0, s.SevenDay!.Utilization);
        Assert.Equal(5.0, s.SevenDayOpus!.Utilization);
        Assert.Null(s.SevenDaySonnet);
        Assert.Equal(FetchedAt, s.FetchedAt);
    }

    [Fact]
    public void MissingPerModelFieldsAreNull()
    {
        var json = """{ "five_hour": { "utilization": 1, "resets_at": "2026-09-03T14:00:00Z" }, "seven_day": { "utilization": 2, "resets_at": "2026-09-06T08:00:00Z" } }""";
        var ok = Assert.IsType<UsageResult.Ok>(UsageResponseParser.Parse(json, FetchedAt));
        Assert.Null(ok.Snapshot.SevenDayOpus);
        Assert.Null(ok.Snapshot.SevenDaySonnet);
    }

    [Theory]
    [InlineData(150.0, 100.0)]
    [InlineData(-5.0, 0.0)]
    public void ClampsUtilization(double raw, double expected)
    {
        var json = $$"""{ "five_hour": { "utilization": {{raw}}, "resets_at": "2026-09-03T14:00:00Z" } }""";
        var ok = Assert.IsType<UsageResult.Ok>(UsageResponseParser.Parse(json, FetchedAt));
        Assert.Equal(expected, ok.Snapshot.FiveHour!.Utilization);
    }

    [Fact]
    public void NullResetKeepsUtilization()
    {
        var json = """{ "five_hour": { "utilization": 0, "resets_at": null } }""";
        var ok = Assert.IsType<UsageResult.Ok>(UsageResponseParser.Parse(json, FetchedAt));
        Assert.Equal(0.0, ok.Snapshot.FiveHour!.Utilization);
        Assert.Null(ok.Snapshot.FiveHour.ResetsAt);
    }

    [Fact]
    public void EmptyObjectIsOkWithNoWindows()
    {
        var ok = Assert.IsType<UsageResult.Ok>(UsageResponseParser.Parse("{}", FetchedAt));
        Assert.Empty(ok.Snapshot.Windows);
        Assert.Null(ok.Snapshot.NextReset);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("")]
    public void BadJsonFails(string json)
    {
        Assert.IsType<UsageResult.Failed>(UsageResponseParser.Parse(json, FetchedAt));
    }
}
```

`tests/ClaudeToolbar.Core.Tests/Usage/UsageSnapshotTests.cs`:
```csharp
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Tests.Usage;

public class UsageSnapshotTests
{
    [Fact]
    public void NextResetIsEarliestNonNull()
    {
        var t = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var s = new UsageSnapshot(
            new UsageWindow(1, t.AddHours(3)),
            new UsageWindow(2, t.AddDays(2)),
            new UsageWindow(3, null),
            null,
            t);
        Assert.Equal(t.AddHours(3), s.NextReset);
        Assert.Equal(3, s.Windows.Count());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~Usage"`
Expected: build errors.

- [ ] **Step 3: Implement**

`src/ClaudeToolbar.Core/Usage/UsageWindow.cs`:
```csharp
namespace ClaudeToolbar.Core.Usage;

/// <summary>One rate-limit window. Utilization is 0..100. ResetsAt is null when the API gives no reset.</summary>
public sealed record UsageWindow(double Utilization, DateTimeOffset? ResetsAt);
```

`src/ClaudeToolbar.Core/Usage/UsageSnapshot.cs`:
```csharp
namespace ClaudeToolbar.Core.Usage;

public sealed record UsageSnapshot(
    UsageWindow? FiveHour,
    UsageWindow? SevenDay,
    UsageWindow? SevenDayOpus,
    UsageWindow? SevenDaySonnet,
    DateTimeOffset FetchedAt)
{
    public IEnumerable<UsageWindow> Windows
    {
        get
        {
            if (FiveHour is not null) yield return FiveHour;
            if (SevenDay is not null) yield return SevenDay;
            if (SevenDayOpus is not null) yield return SevenDayOpus;
            if (SevenDaySonnet is not null) yield return SevenDaySonnet;
        }
    }

    public DateTimeOffset? NextReset
    {
        get
        {
            var resets = Windows.Where(w => w.ResetsAt is not null).Select(w => w.ResetsAt!.Value).ToList();
            return resets.Count == 0 ? null : resets.Min();
        }
    }
}
```

`src/ClaudeToolbar.Core/Usage/UsageResult.cs`:
```csharp
namespace ClaudeToolbar.Core.Usage;

public abstract record UsageResult
{
    private UsageResult() { }

    public sealed record Ok(UsageSnapshot Snapshot) : UsageResult;
    public sealed record Unauthorized : UsageResult;
    public sealed record RateLimited(TimeSpan? RetryAfter) : UsageResult;
    public sealed record Failed(string Message) : UsageResult;
}
```

`src/ClaudeToolbar.Core/Usage/UsageResponseParser.cs`:
```csharp
using System.Globalization;
using System.Text.Json;

namespace ClaudeToolbar.Core.Usage;

public static class UsageResponseParser
{
    public static UsageResult Parse(string json, DateTimeOffset fetchedAt)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new UsageResult.Failed("Empty response");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return new UsageResult.Failed($"Invalid JSON: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new UsageResult.Failed("Response is not a JSON object");

            var snapshot = new UsageSnapshot(
                ReadWindow(root, "five_hour"),
                ReadWindow(root, "seven_day"),
                ReadWindow(root, "seven_day_opus"),
                ReadWindow(root, "seven_day_sonnet"),
                fetchedAt);
            return new UsageResult.Ok(snapshot);
        }
    }

    private static UsageWindow? ReadWindow(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Object)
            return null;
        if (!el.TryGetProperty("utilization", out var u) || u.ValueKind != JsonValueKind.Number)
            return null;

        DateTimeOffset? resetsAt = null;
        if (el.TryGetProperty("resets_at", out var r) && r.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(r.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            resetsAt = parsed;
        }

        return new UsageWindow(Math.Clamp(u.GetDouble(), 0, 100), resetsAt);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~Usage"`
Expected: all pass.

- [ ] **Step 5: Commit and push**

```bash
git add -A
git commit -m "Add usage models and response parser"
git push origin main
```

---

### Task 4: Credentials reader

**Files:**
- Create: `src/ClaudeToolbar.Core/Credentials/CredentialsState.cs`, `ICredentialsSource.cs`, `CredentialsPaths.cs`, `FileCredentialsSource.cs`
- Test: `tests/ClaudeToolbar.Core.Tests/Credentials/CredentialsPathsTests.cs`, `FileCredentialsSourceTests.cs`

**Interfaces:**
- Consumes: `IClock` (Task 2).
- Produces: abstract record `CredentialsState` with nested `Missing(string Path)`, `Invalid(string Path, string Reason)`, `Expired(string Path, DateTimeOffset ExpiresAt, string? SubscriptionType)`, `Valid(string Path, string AccessToken, DateTimeOffset ExpiresAt, string? SubscriptionType)`; `ICredentialsSource { string Path; CredentialsState Read(); }`; `CredentialsPaths.Resolve(string? claudeConfigDir, string userProfile)` and `ResolveFromEnvironment()`; `FileCredentialsSource(string path, IClock clock)` with `Read()` and `Parse(string json)`.

- [ ] **Step 1: Write the failing tests**

`tests/ClaudeToolbar.Core.Tests/Credentials/CredentialsPathsTests.cs`:
```csharp
using ClaudeToolbar.Core.Credentials;

namespace ClaudeToolbar.Core.Tests.Credentials;

public class CredentialsPathsTests
{
    [Fact]
    public void DefaultsToUserProfileClaudeDir()
    {
        var path = CredentialsPaths.Resolve(null, @"C:\Users\me");
        Assert.Equal(Path.Combine(@"C:\Users\me", ".claude", ".credentials.json"), path);
    }

    [Fact]
    public void EnvOverrideWins()
    {
        var path = CredentialsPaths.Resolve(@"D:\cfg", @"C:\Users\me");
        Assert.Equal(Path.Combine(@"D:\cfg", ".credentials.json"), path);
    }

    [Fact]
    public void BlankOverrideIsIgnored()
    {
        var path = CredentialsPaths.Resolve("   ", @"C:\Users\me");
        Assert.Equal(Path.Combine(@"C:\Users\me", ".claude", ".credentials.json"), path);
    }
}
```

`tests/ClaudeToolbar.Core.Tests/Credentials/FileCredentialsSourceTests.cs`:
```csharp
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Tests.Fakes;

namespace ClaudeToolbar.Core.Tests.Credentials;

public class FileCredentialsSourceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ct-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeClock _clock = new(Now);

    public FileCredentialsSourceTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string json)
    {
        var path = Path.Combine(_dir, ".credentials.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string Payload(long expiresAtMs, string token = "sk-ant-oat01-secret") =>
        $$"""{ "claudeAiOauth": { "accessToken": "{{token}}", "refreshToken": "sk-ant-ort01-x", "expiresAt": {{expiresAtMs}}, "scopes": ["user:inference","user:profile"], "subscriptionType": "max" } }""";

    [Fact]
    public void MissingFile()
    {
        var src = new FileCredentialsSource(Path.Combine(_dir, "nope.json"), _clock);
        var state = Assert.IsType<CredentialsState.Missing>(src.Read());
        Assert.EndsWith("nope.json", state.Path);
    }

    [Fact]
    public void ValidToken()
    {
        var path = Write(Payload(Now.AddHours(7).ToUnixTimeMilliseconds()));
        var state = Assert.IsType<CredentialsState.Valid>(new FileCredentialsSource(path, _clock).Read());
        Assert.Equal("sk-ant-oat01-secret", state.AccessToken);
        Assert.Equal("max", state.SubscriptionType);
        Assert.Equal(Now.AddHours(7), state.ExpiresAt);
    }

    [Fact]
    public void ExpiredToken()
    {
        var path = Write(Payload(Now.AddMinutes(-1).ToUnixTimeMilliseconds()));
        var state = Assert.IsType<CredentialsState.Expired>(new FileCredentialsSource(path, _clock).Read());
        Assert.Equal("max", state.SubscriptionType);
    }

    [Fact]
    public void TokenInsideSafetyMarginIsExpired()
    {
        var path = Write(Payload(Now.AddSeconds(30).ToUnixTimeMilliseconds()));
        Assert.IsType<CredentialsState.Expired>(new FileCredentialsSource(path, _clock).Read());
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{}")]
    [InlineData("""{ "claudeAiOauth": { "expiresAt": 1 } }""")]
    [InlineData("""{ "claudeAiOauth": { "accessToken": "x" } }""")]
    public void InvalidShapes(string json)
    {
        var path = Write(json);
        Assert.IsType<CredentialsState.Invalid>(new FileCredentialsSource(path, _clock).Read());
    }

    [Fact]
    public void ToStringNeverContainsToken()
    {
        var path = Write(Payload(Now.AddHours(7).ToUnixTimeMilliseconds(), token: "SUPERSECRET"));
        var state = new FileCredentialsSource(path, _clock).Read();
        Assert.DoesNotContain("SUPERSECRET", state.ToString());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~Credentials"`
Expected: build errors.

- [ ] **Step 3: Implement**

`src/ClaudeToolbar.Core/Credentials/CredentialsState.cs`:
```csharp
using System.Text;

namespace ClaudeToolbar.Core.Credentials;

public abstract record CredentialsState
{
    private CredentialsState() { }

    public sealed record Missing(string Path) : CredentialsState;

    public sealed record Invalid(string Path, string Reason) : CredentialsState;

    public sealed record Expired(string Path, DateTimeOffset ExpiresAt, string? SubscriptionType) : CredentialsState;

    public sealed record Valid(string Path, string AccessToken, DateTimeOffset ExpiresAt, string? SubscriptionType) : CredentialsState
    {
        protected override bool PrintMembers(StringBuilder builder)
        {
            builder.Append("Path = ").Append(Path)
                .Append(", AccessToken = ***, ExpiresAt = ").Append(ExpiresAt)
                .Append(", SubscriptionType = ").Append(SubscriptionType);
            return true;
        }
    }
}
```

`src/ClaudeToolbar.Core/Credentials/ICredentialsSource.cs`:
```csharp
namespace ClaudeToolbar.Core.Credentials;

public interface ICredentialsSource
{
    string Path { get; }
    CredentialsState Read();
}
```

`src/ClaudeToolbar.Core/Credentials/CredentialsPaths.cs`:
```csharp
namespace ClaudeToolbar.Core.Credentials;

public static class CredentialsPaths
{
    public const string FileName = ".credentials.json";
    public const string ConfigDirVariable = "CLAUDE_CONFIG_DIR";

    public static string Resolve(string? claudeConfigDir, string userProfile)
    {
        var dir = string.IsNullOrWhiteSpace(claudeConfigDir)
            ? System.IO.Path.Combine(userProfile, ".claude")
            : claudeConfigDir.Trim();
        return System.IO.Path.Combine(dir, FileName);
    }

    public static string ResolveFromEnvironment() =>
        Resolve(Environment.GetEnvironmentVariable(ConfigDirVariable),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
}
```

`src/ClaudeToolbar.Core/Credentials/FileCredentialsSource.cs`:
```csharp
using System.Text.Json;
using ClaudeToolbar.Core.Time;

namespace ClaudeToolbar.Core.Credentials;

public sealed class FileCredentialsSource : ICredentialsSource
{
    public static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(60);

    private readonly IClock _clock;

    public FileCredentialsSource(string path, IClock clock)
    {
        Path = path;
        _clock = clock;
    }

    public string Path { get; }

    public CredentialsState Read()
    {
        if (!File.Exists(Path))
            return new CredentialsState.Missing(Path);

        string json;
        try
        {
            json = File.ReadAllText(Path);
        }
        catch (IOException ex)
        {
            return new CredentialsState.Invalid(Path, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new CredentialsState.Invalid(Path, ex.Message);
        }

        return Parse(json);
    }

    public CredentialsState Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("claudeAiOauth", out var oauth) ||
                oauth.ValueKind != JsonValueKind.Object)
                return new CredentialsState.Invalid(Path, "claudeAiOauth section missing");

            if (!oauth.TryGetProperty("accessToken", out var tokenEl) ||
                tokenEl.ValueKind != JsonValueKind.String ||
                string.IsNullOrEmpty(tokenEl.GetString()))
                return new CredentialsState.Invalid(Path, "accessToken missing");

            if (!oauth.TryGetProperty("expiresAt", out var expEl) || expEl.ValueKind != JsonValueKind.Number)
                return new CredentialsState.Invalid(Path, "expiresAt missing");

            var expiresMs = expEl.TryGetInt64(out var ms) ? ms : (long)expEl.GetDouble();
            var expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(expiresMs);

            string? subscription = oauth.TryGetProperty("subscriptionType", out var subEl) && subEl.ValueKind == JsonValueKind.String
                ? subEl.GetString()
                : null;

            if (_clock.UtcNow >= expiresAt - ExpiryMargin)
                return new CredentialsState.Expired(Path, expiresAt, subscription);

            return new CredentialsState.Valid(Path, tokenEl.GetString()!, expiresAt, subscription);
        }
        catch (JsonException ex)
        {
            return new CredentialsState.Invalid(Path, $"Invalid JSON: {ex.Message}");
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~Credentials"`
Expected: all pass.

- [ ] **Step 5: Commit and push**

```bash
git add -A
git commit -m "Add credentials file reader with expiry detection"
git push origin main
```

---

### Task 5: Usage HTTP client

**Files:**
- Create: `src/ClaudeToolbar.Core/Usage/IUsageClient.cs`, `UsageClient.cs`
- Test: `tests/ClaudeToolbar.Core.Tests/Usage/UsageClientTests.cs`

**Interfaces:**
- Consumes: `UsageResponseParser`, `UsageResult` (Task 3), `IClock` (Task 2).
- Produces: `IUsageClient { Task<UsageResult> FetchAsync(string accessToken, CancellationToken ct); }`, `UsageClient(HttpClient http, IClock clock)` with constants `Endpoint`, `UserAgent`, `BetaHeader`, `RequestTimeout`.

- [ ] **Step 1: Write the failing tests**

`tests/ClaudeToolbar.Core.Tests/Usage/UsageClientTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Headers;
using ClaudeToolbar.Core.Tests.Fakes;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Tests.Usage;

public class UsageClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Respond(request));
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    private static (UsageClient client, StubHandler handler) Make()
    {
        var handler = new StubHandler();
        return (new UsageClient(new HttpClient(handler), new FakeClock(Now)), handler);
    }

    [Fact]
    public async Task SendsRequiredHeaders()
    {
        var (client, handler) = Make();
        await client.FetchAsync("tok", CancellationToken.None);
        var req = handler.LastRequest!;
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal(UsageClient.Endpoint, req.RequestUri!.ToString());
        Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
        Assert.Equal("tok", req.Headers.Authorization.Parameter);
        Assert.Equal("oauth-2025-04-20", req.Headers.GetValues("anthropic-beta").Single());
        Assert.Equal("claude-code/2.0.0", req.Headers.GetValues("User-Agent").Single());
        Assert.Contains(req.Headers.Accept, a => a.MediaType == "application/json");
    }

    [Fact]
    public async Task OkParsesBody()
    {
        var (client, handler) = Make();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "five_hour": { "utilization": 42, "resets_at": "2026-09-03T14:00:00Z" } }""")
        };
        var ok = Assert.IsType<UsageResult.Ok>(await client.FetchAsync("tok", CancellationToken.None));
        Assert.Equal(42.0, ok.Snapshot.FiveHour!.Utilization);
        Assert.Equal(Now, ok.Snapshot.FetchedAt);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task AuthFailuresAreUnauthorized(HttpStatusCode code)
    {
        var (client, handler) = Make();
        handler.Respond = _ => new HttpResponseMessage(code);
        Assert.IsType<UsageResult.Unauthorized>(await client.FetchAsync("tok", CancellationToken.None));
    }

    [Fact]
    public async Task RateLimitedCarriesRetryAfter()
    {
        var (client, handler) = Make();
        handler.Respond = _ =>
        {
            var r = new HttpResponseMessage((HttpStatusCode)429);
            r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
            return r;
        };
        var rl = Assert.IsType<UsageResult.RateLimited>(await client.FetchAsync("tok", CancellationToken.None));
        Assert.Equal(TimeSpan.FromSeconds(120), rl.RetryAfter);
    }

    [Fact]
    public async Task ServerErrorFails()
    {
        var (client, handler) = Make();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var f = Assert.IsType<UsageResult.Failed>(await client.FetchAsync("tok", CancellationToken.None));
        Assert.Equal("HTTP 500", f.Message);
    }

    [Fact]
    public async Task NetworkErrorFails()
    {
        var (client, handler) = Make();
        handler.Respond = _ => throw new HttpRequestException("boom");
        var f = Assert.IsType<UsageResult.Failed>(await client.FetchAsync("tok", CancellationToken.None));
        Assert.Equal("boom", f.Message);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~UsageClient"`
Expected: build errors.

- [ ] **Step 3: Implement**

`src/ClaudeToolbar.Core/Usage/IUsageClient.cs`:
```csharp
namespace ClaudeToolbar.Core.Usage;

public interface IUsageClient
{
    Task<UsageResult> FetchAsync(string accessToken, CancellationToken cancellationToken);
}
```

`src/ClaudeToolbar.Core/Usage/UsageClient.cs`:
```csharp
using System.Net;
using System.Net.Http.Headers;
using ClaudeToolbar.Core.Time;

namespace ClaudeToolbar.Core.Usage;

public sealed class UsageClient : IUsageClient
{
    public const string Endpoint = "https://api.anthropic.com/api/oauth/usage";
    public const string UserAgent = "claude-code/2.0.0";
    public const string BetaHeader = "oauth-2025-04-20";
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly IClock _clock;

    public UsageClient(HttpClient http, IClock clock)
    {
        _http = http;
        _clock = clock;
    }

    public async Task<UsageResult> FetchAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("anthropic-beta", BetaHeader);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);

        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new UsageResult.Unauthorized();

            if ((int)response.StatusCode == 429)
                return new UsageResult.RateLimited(ReadRetryAfter(response));

            if (!response.IsSuccessStatusCode)
                return new UsageResult.Failed($"HTTP {(int)response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            return UsageResponseParser.Parse(json, _clock.UtcNow);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new UsageResult.Failed("Request timed out");
        }
        catch (HttpRequestException ex)
        {
            return new UsageResult.Failed(ex.Message);
        }
    }

    private TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header is null) return null;
        if (header.Delta is { } delta) return delta;
        if (header.Date is { } date) return date - _clock.UtcNow;
        return null;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~UsageClient"`
Expected: all pass.

- [ ] **Step 5: Commit and push**

```bash
git add -A
git commit -m "Add usage API client"
git push origin main
```

---

### Task 6: Settings model, presets, validator, store

**Files:**
- Create: `src/ClaudeToolbar.Core/Settings/AppSettings.cs`, `Presets.cs`, `SettingsValidator.cs`, `SettingsStore.cs`
- Test: `tests/ClaudeToolbar.Core.Tests/Settings/SettingsValidatorTests.cs`, `PresetsTests.cs`, `SettingsStoreTests.cs`

**Interfaces:**
- Produces: mutable classes `AppearanceSettings` (Preset, Background, Text, BarTrack, BarOk, BarWarn, BarCrit as `#AARRGGBB` strings; FontSize, CornerRadius doubles; WarnThreshold, CritThreshold ints), `RowSettings` (ShowFiveHour, ShowSevenDay, ShowSevenDayOpus, ShowSevenDaySonnet, ShowLabel, ShowBar, ShowPercent, ShowTime bools; BarWidth double), `BehaviorSettings` (RefreshIntervalSeconds, TrayGapPx ints; HideInFullscreen, RunAtStartup bools), `AppSettings` (Version, Appearance, Rows, Behavior; `CreateDefault()`, `Clone()`); `SettingsJson.Serialize/Deserialize`; `Presets.Names`, `Presets.TryApply(string name, AppearanceSettings a)`; `SettingsValidator.Normalize(AppSettings)` (mutates + returns), `SettingsValidator.IsValidColor(string?)`, `SettingsValidator.NormalizeColor(string?, string fallback)`; `SettingsStore(string path)` with `Path`, `Load()`, `Save(AppSettings)`, static `DefaultPath()`.

- [ ] **Step 1: Write the failing tests**

`tests/ClaudeToolbar.Core.Tests/Settings/SettingsValidatorTests.cs`:
```csharp
using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.Core.Tests.Settings;

public class SettingsValidatorTests
{
    [Fact]
    public void DefaultsAreAlreadyNormal()
    {
        var s = SettingsValidator.Normalize(AppSettings.CreateDefault());
        Assert.Equal("dark", s.Appearance.Preset);
        Assert.Equal("#CC1E1E1E", s.Appearance.Background);
        Assert.Equal(11, s.Appearance.FontSize);
        Assert.Equal(70, s.Appearance.WarnThreshold);
        Assert.Equal(90, s.Appearance.CritThreshold);
        Assert.Equal(60, s.Rows.BarWidth);
        Assert.Equal(60, s.Behavior.RefreshIntervalSeconds);
        Assert.Equal(8, s.Behavior.TrayGapPx);
        Assert.True(s.Behavior.RunAtStartup);
        Assert.True(s.Behavior.HideInFullscreen);
    }

    [Fact]
    public void ClampsRanges()
    {
        var s = AppSettings.CreateDefault();
        s.Appearance.FontSize = 40;
        s.Appearance.CornerRadius = -3;
        s.Rows.BarWidth = 5;
        s.Behavior.RefreshIntervalSeconds = 1;
        s.Behavior.TrayGapPx = 500;
        SettingsValidator.Normalize(s);
        Assert.Equal(14, s.Appearance.FontSize);
        Assert.Equal(0, s.Appearance.CornerRadius);
        Assert.Equal(30, s.Rows.BarWidth);
        Assert.Equal(30, s.Behavior.RefreshIntervalSeconds);
        Assert.Equal(24, s.Behavior.TrayGapPx);
    }

    [Fact]
    public void WarnAtOrAboveCritResetsBoth()
    {
        var s = AppSettings.CreateDefault();
        s.Appearance.WarnThreshold = 95;
        s.Appearance.CritThreshold = 90;
        SettingsValidator.Normalize(s);
        Assert.Equal(70, s.Appearance.WarnThreshold);
        Assert.Equal(90, s.Appearance.CritThreshold);
    }

    [Fact]
    public void BadColorsFallBackToDefaults()
    {
        var s = AppSettings.CreateDefault();
        s.Appearance.Background = "red";
        s.Appearance.Text = "#12345";
        s.Appearance.BarOk = "#00FF00";
        SettingsValidator.Normalize(s);
        Assert.Equal("#CC1E1E1E", s.Appearance.Background);
        Assert.Equal("#FFF3F3F3", s.Appearance.Text);
        Assert.Equal("#FF00FF00", s.Appearance.BarOk);
    }

    [Fact]
    public void NullSectionsAreReplaced()
    {
        var s = SettingsJson.Deserialize("""{ "version": 1, "appearance": null }""");
        SettingsValidator.Normalize(s);
        Assert.NotNull(s.Appearance);
        Assert.NotNull(s.Rows);
        Assert.NotNull(s.Behavior);
    }

    [Theory]
    [InlineData("#CC1E1E1E", true)]
    [InlineData("#cc1e1e1e", true)]
    [InlineData("#1E1E1E", false)]
    [InlineData("CC1E1E1E", false)]
    [InlineData(null, false)]
    public void ValidatesColors(string? value, bool expected)
    {
        Assert.Equal(expected, SettingsValidator.IsValidColor(value));
    }
}
```

`tests/ClaudeToolbar.Core.Tests/Settings/PresetsTests.cs`:
```csharp
using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.Core.Tests.Settings;

public class PresetsTests
{
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    [InlineData("claude")]
    [InlineData("mono")]
    public void EveryPresetProducesValidColors(string name)
    {
        var a = new AppearanceSettings();
        Assert.True(Presets.TryApply(name, a));
        Assert.Equal(name, a.Preset);
        foreach (var c in new[] { a.Background, a.Text, a.BarTrack, a.BarOk, a.BarWarn, a.BarCrit })
            Assert.True(SettingsValidator.IsValidColor(c), c);
    }

    [Fact]
    public void UnknownPresetIsRejected()
    {
        var a = new AppearanceSettings();
        Assert.False(Presets.TryApply("neon", a));
        Assert.Equal("dark", a.Preset);
    }

    [Fact]
    public void NamesListsAllFour()
    {
        Assert.Equal(new[] { "dark", "light", "claude", "mono" }, Presets.Names);
    }
}
```

`tests/ClaudeToolbar.Core.Tests/Settings/SettingsStoreTests.cs`:
```csharp
using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.Core.Tests.Settings;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ct-settings-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void MissingFileGivesDefaultsWithoutCreatingIt()
    {
        var store = new SettingsStore(FilePath);
        var s = store.Load();
        Assert.Equal(60, s.Behavior.RefreshIntervalSeconds);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void RoundTrips()
    {
        var store = new SettingsStore(FilePath);
        var s = AppSettings.CreateDefault();
        s.Appearance.Background = "#80FF0000";
        s.Rows.ShowSevenDayOpus = true;
        s.Behavior.RefreshIntervalSeconds = 120;
        store.Save(s);
        var back = store.Load();
        Assert.Equal("#80FF0000", back.Appearance.Background);
        Assert.True(back.Rows.ShowSevenDayOpus);
        Assert.Equal(120, back.Behavior.RefreshIntervalSeconds);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void UnknownFieldsAreIgnoredAndMissingFieldsDefault()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "version": 1, "future": { "x": 1 }, "behavior": { "trayGapPx": 12 } }""");
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(12, s.Behavior.TrayGapPx);
        Assert.Equal(60, s.Behavior.RefreshIntervalSeconds);
        Assert.Equal("dark", s.Appearance.Preset);
    }

    [Fact]
    public void CorruptFileIsBackedUpAndReplaced()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ this is not json");
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(60, s.Behavior.RefreshIntervalSeconds);
        Assert.True(File.Exists(FilePath + ".bad"));
        Assert.Contains("\"version\"", File.ReadAllText(FilePath));
    }

    [Fact]
    public void CloneIsIndependent()
    {
        var a = AppSettings.CreateDefault();
        var b = a.Clone();
        b.Appearance.FontSize = 13;
        Assert.Equal(11, a.Appearance.FontSize);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~Settings"`
Expected: build errors.

- [ ] **Step 3: Implement**

`src/ClaudeToolbar.Core/Settings/AppSettings.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeToolbar.Core.Settings;

public sealed class AppearanceSettings
{
    public string Preset { get; set; } = "dark";
    public string Background { get; set; } = "#CC1E1E1E";
    public string Text { get; set; } = "#FFF3F3F3";
    public string BarTrack { get; set; } = "#33FFFFFF";
    public string BarOk { get; set; } = "#FF3FB950";
    public string BarWarn { get; set; } = "#FFD29922";
    public string BarCrit { get; set; } = "#FFF85149";
    public double FontSize { get; set; } = 11;
    public double CornerRadius { get; set; } = 6;
    public int WarnThreshold { get; set; } = 70;
    public int CritThreshold { get; set; } = 90;
}

public sealed class RowSettings
{
    public bool ShowFiveHour { get; set; } = true;
    public bool ShowSevenDay { get; set; } = true;
    public bool ShowSevenDayOpus { get; set; }
    public bool ShowSevenDaySonnet { get; set; }
    public bool ShowLabel { get; set; } = true;
    public bool ShowBar { get; set; } = true;
    public bool ShowPercent { get; set; } = true;
    public bool ShowTime { get; set; } = true;
    public double BarWidth { get; set; } = 60;
}

public sealed class BehaviorSettings
{
    public int RefreshIntervalSeconds { get; set; } = 60;
    public int TrayGapPx { get; set; } = 8;
    public bool HideInFullscreen { get; set; } = true;
    public bool RunAtStartup { get; set; } = true;
}

public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public AppearanceSettings Appearance { get; set; } = new();
    public RowSettings Rows { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();

    public static AppSettings CreateDefault() => new();

    public AppSettings Clone() => SettingsJson.Deserialize(SettingsJson.Serialize(this));
}

public static class SettingsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, Options);

    public static AppSettings Deserialize(string json) =>
        JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
}
```

`src/ClaudeToolbar.Core/Settings/Presets.cs`:
```csharp
namespace ClaudeToolbar.Core.Settings;

public static class Presets
{
    public const string Custom = "custom";

    public static IReadOnlyList<string> Names { get; } = ["dark", "light", "claude", "mono"];

    public static bool TryApply(string name, AppearanceSettings a)
    {
        var key = name.Trim().ToLowerInvariant();
        switch (key)
        {
            case "dark":
                Set(a, "#CC1E1E1E", "#FFF3F3F3", "#33FFFFFF", "#FF3FB950", "#FFD29922", "#FFF85149");
                break;
            case "light":
                Set(a, "#CCF7F7F7", "#FF1B1B1B", "#22000000", "#FF1A7F37", "#FFB08800", "#FFCF222E");
                break;
            case "claude":
                Set(a, "#CC1F1A17", "#FFF5EDE4", "#33F5EDE4", "#FFD97757", "#FFE8A34F", "#FFE5484D");
                break;
            case "mono":
                Set(a, "#CC111111", "#FFEDEDED", "#22FFFFFF", "#FFBDBDBD", "#FF8A8A8A", "#FFFFFFFF");
                break;
            default:
                return false;
        }

        a.Preset = key;
        return true;
    }

    private static void Set(AppearanceSettings a, string background, string text, string track, string ok, string warn, string crit)
    {
        a.Background = background;
        a.Text = text;
        a.BarTrack = track;
        a.BarOk = ok;
        a.BarWarn = warn;
        a.BarCrit = crit;
    }
}
```

`src/ClaudeToolbar.Core/Settings/SettingsValidator.cs`:
```csharp
using System.Text.RegularExpressions;

namespace ClaudeToolbar.Core.Settings;

public static partial class SettingsValidator
{
    [GeneratedRegex("^#[0-9A-Fa-f]{8}$")]
    private static partial Regex ArgbPattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex RgbPattern();

    public static bool IsValidColor(string? value) => value is not null && ArgbPattern().IsMatch(value);

    /// <summary>Returns a valid #AARRGGBB string: accepts #AARRGGBB, expands #RRGGBB to opaque, else falls back.</summary>
    public static string NormalizeColor(string? value, string fallback)
    {
        if (IsValidColor(value)) return value!.ToUpperInvariant();
        if (value is not null && RgbPattern().IsMatch(value)) return ("#FF" + value[1..]).ToUpperInvariant();
        return fallback;
    }

    public static AppSettings Normalize(AppSettings s)
    {
        if (s.Appearance is null) s.Appearance = new AppearanceSettings();
        if (s.Rows is null) s.Rows = new RowSettings();
        if (s.Behavior is null) s.Behavior = new BehaviorSettings();

        var a = s.Appearance;
        var d = new AppearanceSettings();
        a.Background = NormalizeColor(a.Background, d.Background);
        a.Text = NormalizeColor(a.Text, d.Text);
        a.BarTrack = NormalizeColor(a.BarTrack, d.BarTrack);
        a.BarOk = NormalizeColor(a.BarOk, d.BarOk);
        a.BarWarn = NormalizeColor(a.BarWarn, d.BarWarn);
        a.BarCrit = NormalizeColor(a.BarCrit, d.BarCrit);
        a.FontSize = Math.Clamp(a.FontSize, 9, 14);
        a.CornerRadius = Math.Clamp(a.CornerRadius, 0, 12);
        a.WarnThreshold = Math.Clamp(a.WarnThreshold, 1, 99);
        a.CritThreshold = Math.Clamp(a.CritThreshold, 2, 100);
        if (a.WarnThreshold >= a.CritThreshold)
        {
            a.WarnThreshold = d.WarnThreshold;
            a.CritThreshold = d.CritThreshold;
        }
        if (string.IsNullOrWhiteSpace(a.Preset)) a.Preset = Presets.Custom;
        a.Preset = a.Preset.Trim().ToLowerInvariant();

        s.Rows.BarWidth = Math.Clamp(s.Rows.BarWidth, 30, 120);

        var b = s.Behavior;
        b.RefreshIntervalSeconds = Math.Clamp(b.RefreshIntervalSeconds, 30, 300);
        b.TrayGapPx = Math.Clamp(b.TrayGapPx, 0, 24);

        s.Version = 1;
        return s;
    }
}
```

`src/ClaudeToolbar.Core/Settings/SettingsStore.cs`:
```csharp
using System.Text.Json;

namespace ClaudeToolbar.Core.Settings;

public sealed class SettingsStore
{
    public SettingsStore(string path) => Path = path;

    public string Path { get; }

    public static string DefaultPath() => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeToolbar", "settings.json");

    public AppSettings Load()
    {
        if (!File.Exists(Path))
            return SettingsValidator.Normalize(AppSettings.CreateDefault());

        try
        {
            var json = File.ReadAllText(Path);
            return SettingsValidator.Normalize(SettingsJson.Deserialize(json));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            TryBackupBad();
            var defaults = SettingsValidator.Normalize(AppSettings.CreateDefault());
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, SettingsJson.Serialize(SettingsValidator.Normalize(settings)));
        File.Move(tmp, Path, overwrite: true);
    }

    private void TryBackupBad()
    {
        try
        {
            File.Copy(Path, Path + ".bad", overwrite: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~Settings"`
Expected: all pass. If the compiler warns that `s.Appearance is null` is always false, keep the check (JSON can set it to null) and silence with `#pragma warning disable CS8073` is NOT needed; the check compiles cleanly under nullable.

- [ ] **Step 5: Commit and push**

```bash
git add -A
git commit -m "Add settings model, presets, validation and atomic store"
git push origin main
```

---

### Task 7: Refresh scheduler

**Files:**
- Create: `src/ClaudeToolbar.Core/Refresh/RefreshScheduler.cs`
- Test: `tests/ClaudeToolbar.Core.Tests/Refresh/RefreshSchedulerTests.cs`

**Interfaces:**
- Consumes: `IClock`.
- Produces: `RefreshScheduler(IClock clock, int intervalSeconds = 60)` with `int IntervalSeconds {get;set;}`, `DateTimeOffset? NextDue`, `bool IsPaused`, `TimeSpan CurrentBackoff`, `bool IsDue(DateTimeOffset now)`, `RequestImmediate()`, `Pause()`, `OnSuccess(DateTimeOffset? nextReset)`, `OnFailure(TimeSpan? retryAfter)`; constants `InitialBackoff` (15 s), `MaxBackoff` (300 s).

- [ ] **Step 1: Write the failing tests**

`tests/ClaudeToolbar.Core.Tests/Refresh/RefreshSchedulerTests.cs`:
```csharp
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Tests.Fakes;

namespace ClaudeToolbar.Core.Tests.Refresh;

public class RefreshSchedulerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DueImmediatelyAtStart()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock);
        Assert.True(s.IsDue(clock.UtcNow));
    }

    [Fact]
    public void SuccessSchedulesOneInterval()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60);
        s.OnSuccess(null);
        Assert.Equal(T0.AddSeconds(60), s.NextDue);
        Assert.False(s.IsDue(T0.AddSeconds(59)));
        Assert.True(s.IsDue(T0.AddSeconds(60)));
    }

    [Fact]
    public void SuccessUsesUpcomingResetWhenSooner()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60);
        s.OnSuccess(T0.AddSeconds(20));
        Assert.Equal(T0.AddSeconds(21), s.NextDue);
    }

    [Fact]
    public void PastResetIsIgnored()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60);
        s.OnSuccess(T0.AddSeconds(-5));
        Assert.Equal(T0.AddSeconds(60), s.NextDue);
    }

    [Fact]
    public void FailureBacksOffDoublingToCap()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60);
        var expected = new[] { 15, 30, 60, 120, 240, 300, 300 };
        foreach (var seconds in expected)
        {
            s.OnFailure(null);
            Assert.Equal(TimeSpan.FromSeconds(seconds), s.CurrentBackoff);
            Assert.Equal(clock.UtcNow.AddSeconds(seconds), s.NextDue);
        }
    }

    [Fact]
    public void RetryAfterWinsWhenLarger()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60);
        s.OnFailure(TimeSpan.FromSeconds(100));
        Assert.Equal(T0.AddSeconds(100), s.NextDue);
        s.OnFailure(TimeSpan.FromSeconds(5));
        Assert.Equal(T0.AddSeconds(30), s.NextDue);
    }

    [Fact]
    public void SuccessResetsBackoff()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60);
        s.OnFailure(null);
        s.OnFailure(null);
        s.OnSuccess(null);
        Assert.Equal(TimeSpan.Zero, s.CurrentBackoff);
        s.OnFailure(null);
        Assert.Equal(TimeSpan.FromSeconds(15), s.CurrentBackoff);
    }

    [Fact]
    public void PauseAndImmediate()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60);
        s.Pause();
        Assert.True(s.IsPaused);
        Assert.False(s.IsDue(T0.AddDays(1)));
        s.RequestImmediate();
        Assert.False(s.IsPaused);
        Assert.True(s.IsDue(clock.UtcNow));
    }

    [Fact]
    public void IntervalChangeAppliesOnNextSuccess()
    {
        var clock = new FakeClock(T0);
        var s = new RefreshScheduler(clock, 60) { IntervalSeconds = 120 };
        s.OnSuccess(null);
        Assert.Equal(T0.AddSeconds(120), s.NextDue);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~RefreshScheduler"`
Expected: build errors.

- [ ] **Step 3: Implement**

`src/ClaudeToolbar.Core/Refresh/RefreshScheduler.cs`:
```csharp
using ClaudeToolbar.Core.Time;

namespace ClaudeToolbar.Core.Refresh;

/// <summary>Decides when the next usage fetch is due. Not thread-safe; call from one thread.</summary>
public sealed class RefreshScheduler
{
    public static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(300);
    private static readonly TimeSpan ResetGrace = TimeSpan.FromSeconds(1);

    private readonly IClock _clock;

    public RefreshScheduler(IClock clock, int intervalSeconds = 60)
    {
        _clock = clock;
        IntervalSeconds = intervalSeconds;
        NextDue = clock.UtcNow;
    }

    public int IntervalSeconds { get; set; }

    public DateTimeOffset? NextDue { get; private set; }

    public bool IsPaused => NextDue is null;

    public TimeSpan CurrentBackoff { get; private set; } = TimeSpan.Zero;

    public bool IsDue(DateTimeOffset now) => NextDue is { } due && now >= due;

    public void RequestImmediate() => NextDue = _clock.UtcNow;

    public void Pause() => NextDue = null;

    public void OnSuccess(DateTimeOffset? nextReset)
    {
        CurrentBackoff = TimeSpan.Zero;
        var now = _clock.UtcNow;
        var due = now + TimeSpan.FromSeconds(IntervalSeconds);
        if (nextReset is { } reset)
        {
            var afterReset = reset + ResetGrace;
            if (afterReset > now && afterReset < due) due = afterReset;
        }
        NextDue = due;
    }

    public void OnFailure(TimeSpan? retryAfter)
    {
        CurrentBackoff = CurrentBackoff == TimeSpan.Zero
            ? InitialBackoff
            : TimeSpan.FromTicks(Math.Min((CurrentBackoff * 2).Ticks, MaxBackoff.Ticks));
        var delay = retryAfter is { } ra && ra > CurrentBackoff ? ra : CurrentBackoff;
        NextDue = _clock.UtcNow + delay;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~RefreshScheduler"`
Expected: all pass.

- [ ] **Step 5: Commit and push**

```bash
git add -A
git commit -m "Add refresh scheduler with backoff"
git push origin main
```

---

### Task 8: Usage monitor

**Files:**
- Create: `src/ClaudeToolbar.Core/Refresh/MonitorState.cs`, `UsageMonitor.cs`
- Create: `tests/ClaudeToolbar.Core.Tests/Fakes/FakeUsageClient.cs`, `FakeCredentialsSource.cs`
- Test: `tests/ClaudeToolbar.Core.Tests/Refresh/UsageMonitorTests.cs`

**Interfaces:**
- Consumes: `ICredentialsSource`, `CredentialsState` (Task 4), `IUsageClient`, `UsageResult`, `UsageSnapshot` (Tasks 3, 5), `RefreshScheduler` (Task 7), `IClock`.
- Produces: `enum UsageStatus { Loading, Ok, Stale, Expired, NoCredentials }`; `record MonitorState(UsageStatus Status, UsageSnapshot? Snapshot, DateTimeOffset? LastSuccess, string? Message, CredentialsState Credentials)`; `UsageMonitor(ICredentialsSource, IUsageClient, IClock, RefreshScheduler)` with `MonitorState State`, `RefreshScheduler Scheduler`, `event Action<MonitorState>? StateChanged` (raised on whatever thread the fetch completes on), `RequestRefresh()`, `OnCredentialsChanged()`, `Task TickAsync(CancellationToken)`, `Task RefreshAsync(CancellationToken)`.

- [ ] **Step 1: Write fakes and failing tests**

`tests/ClaudeToolbar.Core.Tests/Fakes/FakeUsageClient.cs`:
```csharp
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Tests.Fakes;

public sealed class FakeUsageClient : IUsageClient
{
    public Queue<UsageResult> Results { get; } = new();
    public List<string> Tokens { get; } = new();
    public TaskCompletionSource<UsageResult>? Pending { get; set; }

    public Task<UsageResult> FetchAsync(string accessToken, CancellationToken cancellationToken)
    {
        Tokens.Add(accessToken);
        if (Pending is not null) return Pending.Task;
        return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : new UsageResult.Failed("no canned result"));
    }
}
```

`tests/ClaudeToolbar.Core.Tests/Fakes/FakeCredentialsSource.cs`:
```csharp
using ClaudeToolbar.Core.Credentials;

namespace ClaudeToolbar.Core.Tests.Fakes;

public sealed class FakeCredentialsSource : ICredentialsSource
{
    public string Path { get; } = @"C:\fake\.credentials.json";
    public CredentialsState State { get; set; }

    public FakeCredentialsSource(CredentialsState? initial = null) =>
        State = initial ?? new CredentialsState.Missing(Path);

    public CredentialsState Read() => State;
}
```

`tests/ClaudeToolbar.Core.Tests/Refresh/UsageMonitorTests.cs`:
```csharp
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Tests.Fakes;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Tests.Refresh;

public class UsageMonitorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(T0);
    private readonly FakeUsageClient _client = new();
    private readonly FakeCredentialsSource _creds = new();
    private readonly RefreshScheduler _scheduler;
    private readonly UsageMonitor _monitor;
    private readonly List<MonitorState> _published = new();

    public UsageMonitorTests()
    {
        _scheduler = new RefreshScheduler(_clock, 60);
        _monitor = new UsageMonitor(_creds, _client, _clock, _scheduler);
        _monitor.StateChanged += s => _published.Add(s);
    }

    private static CredentialsState.Valid ValidCreds(DateTimeOffset now) =>
        new(@"C:\fake\.credentials.json", "tok", now.AddHours(7), "max");

    private static UsageSnapshot Snapshot(DateTimeOffset now, double five = 42, double seven = 18) =>
        new(new UsageWindow(five, now.AddHours(2)), new UsageWindow(seven, now.AddDays(3)), null, null, now);

    [Fact]
    public async Task MissingCredentialsPausesWithNoCredentials()
    {
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(UsageStatus.NoCredentials, _monitor.State.Status);
        Assert.True(_scheduler.IsPaused);
        Assert.Empty(_client.Tokens);
    }

    [Fact]
    public async Task ValidCredentialsFetchAndPublishOk()
    {
        _creds.State = ValidCreds(T0);
        _client.Results.Enqueue(new UsageResult.Ok(Snapshot(T0)));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(UsageStatus.Ok, _monitor.State.Status);
        Assert.Equal(42.0, _monitor.State.Snapshot!.FiveHour!.Utilization);
        Assert.Equal(T0, _monitor.State.LastSuccess);
        Assert.Equal(new[] { "tok" }, _client.Tokens);
        Assert.Equal(T0.AddSeconds(60), _scheduler.NextDue);
        Assert.Single(_published);
    }

    [Fact]
    public async Task NotDueMeansNoFetch()
    {
        _creds.State = ValidCreds(T0);
        _client.Results.Enqueue(new UsageResult.Ok(Snapshot(T0)));
        await _monitor.TickAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(30));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Single(_client.Tokens);
    }

    [Fact]
    public async Task FailureWithSnapshotIsStaleAndKeepsData()
    {
        _creds.State = ValidCreds(T0);
        _client.Results.Enqueue(new UsageResult.Ok(Snapshot(T0)));
        await _monitor.TickAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(60));
        _client.Results.Enqueue(new UsageResult.Failed("net down"));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(UsageStatus.Stale, _monitor.State.Status);
        Assert.NotNull(_monitor.State.Snapshot);
        Assert.Equal("net down", _monitor.State.Message);
        Assert.Equal(TimeSpan.FromSeconds(15), _scheduler.CurrentBackoff);
    }

    [Fact]
    public async Task FailureWithoutSnapshotStaysLoading()
    {
        _creds.State = ValidCreds(T0);
        _client.Results.Enqueue(new UsageResult.Failed("net down"));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(UsageStatus.Loading, _monitor.State.Status);
        Assert.Null(_monitor.State.Snapshot);
    }

    [Fact]
    public async Task UnauthorizedPausesUntilCredentialsChange()
    {
        _creds.State = ValidCreds(T0);
        _client.Results.Enqueue(new UsageResult.Unauthorized());
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(UsageStatus.Expired, _monitor.State.Status);
        Assert.True(_scheduler.IsPaused);

        _monitor.OnCredentialsChanged();
        _client.Results.Enqueue(new UsageResult.Ok(Snapshot(T0)));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(UsageStatus.Ok, _monitor.State.Status);
    }

    [Fact]
    public async Task ExpiredCredentialsKeepPreviousSnapshot()
    {
        _creds.State = ValidCreds(T0);
        _client.Results.Enqueue(new UsageResult.Ok(Snapshot(T0)));
        await _monitor.TickAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(60));
        _creds.State = new CredentialsState.Expired(_creds.Path, T0, "max");
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(UsageStatus.Expired, _monitor.State.Status);
        Assert.NotNull(_monitor.State.Snapshot);
        Assert.True(_scheduler.IsPaused);
    }

    [Fact]
    public async Task PassedResetTriggersOneImmediateRefresh()
    {
        _creds.State = ValidCreds(T0);
        var snap = new UsageSnapshot(new UsageWindow(42, T0.AddSeconds(10)), null, null, null, T0);
        _client.Results.Enqueue(new UsageResult.Ok(snap));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(T0.AddSeconds(11), _scheduler.NextDue);

        _clock.Advance(TimeSpan.FromSeconds(11));
        _client.Results.Enqueue(new UsageResult.Ok(snap));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(2, _client.Tokens.Count);

        _clock.Advance(TimeSpan.FromSeconds(1));
        await _monitor.TickAsync(CancellationToken.None);
        Assert.Equal(2, _client.Tokens.Count);
    }

    [Fact]
    public async Task ConcurrentRefreshIsIgnored()
    {
        _creds.State = ValidCreds(T0);
        _client.Pending = new TaskCompletionSource<UsageResult>();
        var first = _monitor.RefreshAsync(CancellationToken.None);
        var second = _monitor.RefreshAsync(CancellationToken.None);
        await second;
        Assert.Single(_client.Tokens);
        _client.Pending.SetResult(new UsageResult.Ok(Snapshot(T0)));
        await first;
        Assert.Equal(UsageStatus.Ok, _monitor.State.Status);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~UsageMonitor"`
Expected: build errors.

- [ ] **Step 3: Implement**

`src/ClaudeToolbar.Core/Refresh/MonitorState.cs`:
```csharp
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Refresh;

public enum UsageStatus
{
    Loading,
    Ok,
    Stale,
    Expired,
    NoCredentials,
}

public sealed record MonitorState(
    UsageStatus Status,
    UsageSnapshot? Snapshot,
    DateTimeOffset? LastSuccess,
    string? Message,
    CredentialsState Credentials)
{
    public static MonitorState Initial(CredentialsState credentials) =>
        new(UsageStatus.Loading, null, null, null, credentials);
}
```

`src/ClaudeToolbar.Core/Refresh/UsageMonitor.cs`:
```csharp
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Time;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Refresh;

/// <summary>Owns the fetch loop state machine. The host calls <see cref="TickAsync"/> about once a second.</summary>
public sealed class UsageMonitor
{
    private readonly ICredentialsSource _credentials;
    private readonly IUsageClient _client;
    private readonly IClock _clock;
    private int _refreshing;
    private DateTimeOffset? _resetTriggeredFor;

    public UsageMonitor(ICredentialsSource credentials, IUsageClient client, IClock clock, RefreshScheduler scheduler)
    {
        _credentials = credentials;
        _client = client;
        _clock = clock;
        Scheduler = scheduler;
        State = MonitorState.Initial(new CredentialsState.Missing(credentials.Path));
    }

    public MonitorState State { get; private set; }

    public RefreshScheduler Scheduler { get; }

    public event Action<MonitorState>? StateChanged;

    public void RequestRefresh() => Scheduler.RequestImmediate();

    public void OnCredentialsChanged() => Scheduler.RequestImmediate();

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        if (State.Snapshot?.NextReset is { } reset && reset <= now && _resetTriggeredFor != reset)
        {
            _resetTriggeredFor = reset;
            Scheduler.RequestImmediate();
        }

        if (!Scheduler.IsDue(now)) return;
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _refreshing, 1) == 1) return;
        try
        {
            var creds = _credentials.Read();
            switch (creds)
            {
                case CredentialsState.Missing:
                    Scheduler.Pause();
                    Publish(State with { Status = UsageStatus.NoCredentials, Message = "Credentials file not found", Credentials = creds });
                    return;
                case CredentialsState.Invalid invalid:
                    Scheduler.Pause();
                    Publish(State with { Status = UsageStatus.NoCredentials, Message = invalid.Reason, Credentials = creds });
                    return;
                case CredentialsState.Expired:
                    Scheduler.Pause();
                    Publish(State with { Status = UsageStatus.Expired, Message = "Login expired", Credentials = creds });
                    return;
                case CredentialsState.Valid valid:
                    var result = await _client.FetchAsync(valid.AccessToken, cancellationToken).ConfigureAwait(false);
                    Apply(result, creds);
                    return;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    private void Apply(UsageResult result, CredentialsState creds)
    {
        switch (result)
        {
            case UsageResult.Ok ok:
                Scheduler.OnSuccess(ok.Snapshot.NextReset);
                Publish(new MonitorState(UsageStatus.Ok, ok.Snapshot, ok.Snapshot.FetchedAt, null, creds));
                break;
            case UsageResult.Unauthorized:
                Scheduler.Pause();
                Publish(State with { Status = UsageStatus.Expired, Message = "Token rejected", Credentials = creds });
                break;
            case UsageResult.RateLimited rl:
                Scheduler.OnFailure(rl.RetryAfter);
                Publish(State with { Status = DegradedStatus(), Message = "Rate limited", Credentials = creds });
                break;
            case UsageResult.Failed f:
                Scheduler.OnFailure(null);
                Publish(State with { Status = DegradedStatus(), Message = f.Message, Credentials = creds });
                break;
        }
    }

    private UsageStatus DegradedStatus() => State.Snapshot is null ? UsageStatus.Loading : UsageStatus.Stale;

    private void Publish(MonitorState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~UsageMonitor"`
Expected: all pass.

- [ ] **Step 5: Commit and push**

```bash
git add -A
git commit -m "Add usage monitor state machine"
git push origin main
```

---

### Task 9: Placement math, widget model, flyout model

**Files:**
- Create: `src/ClaudeToolbar.Core/Layout/RectI.cs`, `WidgetPlacement.cs`
- Create: `src/ClaudeToolbar.Core/Widget/WidgetModel.cs`, `FlyoutModel.cs`
- Test: `tests/ClaudeToolbar.Core.Tests/Layout/WidgetPlacementTests.cs`, `tests/ClaudeToolbar.Core.Tests/Widget/WidgetModelBuilderTests.cs`, `FlyoutModelBuilderTests.cs`

**Interfaces:**
- Consumes: `MonitorState`, `UsageStatus` (Task 8), `AppSettings` (Task 6), formatters (Task 2).
- Produces: `readonly record struct RectI(int Left, int Top, int Right, int Bottom)` with `Width`, `Height`; `WidgetPlacement.Compute(RectI taskbarNow, RectI notifyArea, int widgetWidth, int widgetHeight, int gap) : RectI`, `WidgetPlacement.IsTaskbarMostlyHidden(RectI taskbarNow, RectI monitor) : bool`, `WidgetPlacement.MaxWidgetHeight(RectI taskbar) : int`; `record WidgetRow(string Label, double Utilization, string PercentText, string TimeText, BarLevel Level)`; `record WidgetModel(IReadOnlyList<WidgetRow> Rows, bool Dimmed, bool ShowStaleDot, string? Notice)`; `WidgetModelBuilder.Build(MonitorState, AppSettings, DateTimeOffset now)`; constants `WidgetModelBuilder.SignInNotice`, `RunClaudeHint`; `record FlyoutModel(IReadOnlyList<string> Lines, string StatusText)`; `FlyoutModelBuilder.Build(MonitorState, DateTimeOffset now, Func<DateTimeOffset,string> formatClock)`; `AgoFormatter.Format(TimeSpan)`.

- [ ] **Step 1: Write the failing tests**

`tests/ClaudeToolbar.Core.Tests/Layout/WidgetPlacementTests.cs`:
```csharp
using ClaudeToolbar.Core.Layout;

namespace ClaudeToolbar.Core.Tests.Layout;

public class WidgetPlacementTests
{
    private static readonly RectI Taskbar = new(0, 1392, 2560, 1440);
    private static readonly RectI Notify = new(2200, 1392, 2560, 1440);

    [Fact]
    public void RightEdgeSitsGapLeftOfTrayAndVerticallyCentered()
    {
        var r = WidgetPlacement.Compute(Taskbar, Notify, widgetWidth: 180, widgetHeight: 40, gap: 8);
        Assert.Equal(2192, r.Right);
        Assert.Equal(2012, r.Left);
        Assert.Equal(1396, r.Top);
        Assert.Equal(1436, r.Bottom);
        Assert.Equal(180, r.Width);
        Assert.Equal(40, r.Height);
    }

    [Fact]
    public void OddRemainderRoundsDown()
    {
        var r = WidgetPlacement.Compute(Taskbar, Notify, 100, 41, 0);
        Assert.Equal(1395, r.Top);
    }

    [Fact]
    public void MaxHeightLeavesFourPixels()
    {
        Assert.Equal(44, WidgetPlacement.MaxWidgetHeight(Taskbar));
    }

    [Fact]
    public void HiddenTaskbarDetected()
    {
        var monitor = new RectI(0, 0, 2560, 1440);
        Assert.False(WidgetPlacement.IsTaskbarMostlyHidden(Taskbar, monitor));
        Assert.True(WidgetPlacement.IsTaskbarMostlyHidden(new RectI(0, 1438, 2560, 1486), monitor));
        Assert.False(WidgetPlacement.IsTaskbarMostlyHidden(new RectI(0, 1410, 2560, 1458), monitor));
    }
}
```

`tests/ClaudeToolbar.Core.Tests/Widget/WidgetModelBuilderTests.cs`:
```csharp
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Usage;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.Core.Tests.Widget;

public class WidgetModelBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly CredentialsState Creds = new CredentialsState.Valid("p", "t", Now.AddHours(7), "max");

    private static UsageSnapshot Snap() => new(
        new UsageWindow(42, Now.AddHours(2).AddMinutes(13)),
        new UsageWindow(75, Now.AddDays(3).AddHours(4)),
        new UsageWindow(95, Now.AddDays(3)),
        null,
        Now);

    [Fact]
    public void OkStateBuildsTwoDefaultRows()
    {
        var state = new MonitorState(UsageStatus.Ok, Snap(), Now, null, Creds);
        var m = WidgetModelBuilder.Build(state, AppSettings.CreateDefault(), Now);
        Assert.Equal(2, m.Rows.Count);
        Assert.Equal(new WidgetRow("5h", 42, "42%", "2h 13m", BarLevel.Ok), m.Rows[0]);
        Assert.Equal(new WidgetRow("7d", 75, "75%", "3d 4h", BarLevel.Warn), m.Rows[1]);
        Assert.False(m.Dimmed);
        Assert.False(m.ShowStaleDot);
        Assert.Null(m.Notice);
    }

    [Fact]
    public void PerModelRowsFollowSettings()
    {
        var s = AppSettings.CreateDefault();
        s.Rows.ShowSevenDayOpus = true;
        s.Rows.ShowSevenDaySonnet = true;
        var state = new MonitorState(UsageStatus.Ok, Snap(), Now, null, Creds);
        var m = WidgetModelBuilder.Build(state, s, Now);
        Assert.Equal(4, m.Rows.Count);
        Assert.Equal("7d Opus", m.Rows[2].Label);
        Assert.Equal(BarLevel.Crit, m.Rows[2].Level);
        Assert.Equal("7d Sonnet", m.Rows[3].Label);
        Assert.Equal("—", m.Rows[3].PercentText);
    }

    [Fact]
    public void LoadingShowsDashes()
    {
        var state = new MonitorState(UsageStatus.Loading, null, null, null, Creds);
        var m = WidgetModelBuilder.Build(state, AppSettings.CreateDefault(), Now);
        Assert.All(m.Rows, r => Assert.Equal("—", r.PercentText));
        Assert.All(m.Rows, r => Assert.Equal(0.0, r.Utilization));
    }

    [Fact]
    public void StaleShowsDot()
    {
        var state = new MonitorState(UsageStatus.Stale, Snap(), Now, "net", Creds);
        Assert.True(WidgetModelBuilder.Build(state, AppSettings.CreateDefault(), Now).ShowStaleDot);
    }

    [Fact]
    public void ExpiredDimsAndReplacesFirstTime()
    {
        var state = new MonitorState(UsageStatus.Expired, Snap(), Now, "expired", Creds);
        var m = WidgetModelBuilder.Build(state, AppSettings.CreateDefault(), Now);
        Assert.True(m.Dimmed);
        Assert.Equal(WidgetModelBuilder.RunClaudeHint, m.Rows[0].TimeText);
        Assert.Equal("3d 4h", m.Rows[1].TimeText);
    }

    [Fact]
    public void NoCredentialsShowsSignInNotice()
    {
        var state = new MonitorState(UsageStatus.NoCredentials, null, null, null, new CredentialsState.Missing("p"));
        var m = WidgetModelBuilder.Build(state, AppSettings.CreateDefault(), Now);
        Assert.Empty(m.Rows);
        Assert.Equal(WidgetModelBuilder.SignInNotice, m.Notice);
    }

    [Fact]
    public void ThresholdsComeFromSettings()
    {
        var s = AppSettings.CreateDefault();
        s.Appearance.WarnThreshold = 40;
        s.Appearance.CritThreshold = 50;
        var state = new MonitorState(UsageStatus.Ok, Snap(), Now, null, Creds);
        var m = WidgetModelBuilder.Build(state, s, Now);
        Assert.Equal(BarLevel.Warn, m.Rows[0].Level);
        Assert.Equal(BarLevel.Crit, m.Rows[1].Level);
    }
}
```

`tests/ClaudeToolbar.Core.Tests/Widget/FlyoutModelBuilderTests.cs`:
```csharp
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Usage;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.Core.Tests.Widget;

public class FlyoutModelBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly CredentialsState Creds = new CredentialsState.Valid("p", "t", Now.AddHours(7), "max");
    private static string Clock(DateTimeOffset t) => t.ToString("HH:mm");

    [Fact]
    public void ListsWindowsUpdatedAndStatus()
    {
        var snap = new UsageSnapshot(
            new UsageWindow(42, Now.AddHours(2).AddMinutes(13)),
            new UsageWindow(18, Now.AddDays(3).AddHours(4)),
            new UsageWindow(5, Now.AddDays(3)),
            null,
            Now.AddSeconds(-12));
        var state = new MonitorState(UsageStatus.Ok, snap, Now.AddSeconds(-12), null, Creds);
        var f = FlyoutModelBuilder.Build(state, Now, Clock);
        Assert.Equal("Session 42% · resets in 2h 13m · at 12:13", f.Lines[0]);
        Assert.Equal("Weekly 18% · resets in 3d 4h · at 14:00", f.Lines[1]);
        Assert.Equal("Weekly Opus 5% · resets in 3d 0h · at 10:00", f.Lines[2]);
        Assert.Equal("Updated 12s ago", f.Lines[3]);
        Assert.Equal(4, f.Lines.Count);
        Assert.Equal("OK", f.StatusText);
    }

    [Theory]
    [InlineData(UsageStatus.Stale, "net down", "Stale: net down")]
    [InlineData(UsageStatus.Expired, null, "Login expired — run claude")]
    [InlineData(UsageStatus.NoCredentials, null, "Not signed in — run claude")]
    [InlineData(UsageStatus.Loading, null, "Loading…")]
    public void StatusTexts(UsageStatus status, string? message, string expected)
    {
        var state = new MonitorState(status, null, null, message, Creds);
        Assert.Equal(expected, FlyoutModelBuilder.Build(state, Now, Clock).StatusText);
    }

    [Theory]
    [InlineData(12, "12s")]
    [InlineData(59, "59s")]
    [InlineData(60, "1m")]
    [InlineData(3600, "1h 0m")]
    public void AgoFormats(int seconds, string expected)
    {
        Assert.Equal(expected, AgoFormatter.Format(TimeSpan.FromSeconds(seconds)));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ClaudeToolbar.Core.Tests --filter "FullyQualifiedName~Layout|FullyQualifiedName~Widget"`
Expected: build errors.

- [ ] **Step 3: Implement**

`src/ClaudeToolbar.Core/Layout/RectI.cs`:
```csharp
namespace ClaudeToolbar.Core.Layout;

/// <summary>Integer rectangle in physical pixels, Win32 style (Right/Bottom exclusive).</summary>
public readonly record struct RectI(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
```

`src/ClaudeToolbar.Core/Layout/WidgetPlacement.cs`:
```csharp
namespace ClaudeToolbar.Core.Layout;

public static class WidgetPlacement
{
    public const int VerticalMargin = 4;

    /// <summary>Places the widget so its right edge is <paramref name="gap"/> px left of the notification area, centered in the taskbar.</summary>
    public static RectI Compute(RectI taskbarNow, RectI notifyArea, int widgetWidth, int widgetHeight, int gap)
    {
        var right = notifyArea.Left - gap;
        var left = right - widgetWidth;
        var top = taskbarNow.Top + (taskbarNow.Height - widgetHeight) / 2;
        return new RectI(left, top, right, top + widgetHeight);
    }

    /// <summary>True when less than half of the taskbar is inside the monitor (auto-hide slid away).</summary>
    public static bool IsTaskbarMostlyHidden(RectI taskbarNow, RectI monitor)
    {
        var visibleTop = Math.Max(taskbarNow.Top, monitor.Top);
        var visibleBottom = Math.Min(taskbarNow.Bottom, monitor.Bottom);
        var visible = Math.Max(0, visibleBottom - visibleTop);
        return visible < taskbarNow.Height / 2;
    }

    public static int MaxWidgetHeight(RectI taskbar) => Math.Max(0, taskbar.Height - VerticalMargin);
}
```

`src/ClaudeToolbar.Core/Widget/WidgetModel.cs`:
```csharp
using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Widget;

public sealed record WidgetRow(string Label, double Utilization, string PercentText, string TimeText, BarLevel Level);

public sealed record WidgetModel(IReadOnlyList<WidgetRow> Rows, bool Dimmed, bool ShowStaleDot, string? Notice);

public static class WidgetModelBuilder
{
    public const string SignInNotice = "Sign in with claude";
    public const string RunClaudeHint = "↻ run claude";
    public const string NoRowsNotice = "No rows enabled";
    public const string Placeholder = "—";

    public static WidgetModel Build(MonitorState state, AppSettings settings, DateTimeOffset now)
    {
        if (state.Status == UsageStatus.NoCredentials)
            return new WidgetModel([], false, false, SignInNotice);

        var snapshot = state.Snapshot;
        var a = settings.Appearance;
        var r = settings.Rows;
        var rows = new List<WidgetRow>(4);

        void Add(bool show, string label, UsageWindow? window)
        {
            if (!show) return;
            if (window is null)
            {
                rows.Add(new WidgetRow(label, 0, Placeholder, string.Empty, BarLevel.Ok));
                return;
            }
            var time = window.ResetsAt is { } reset ? RemainingTimeFormatter.Format(reset, now) : string.Empty;
            rows.Add(new WidgetRow(
                label,
                window.Utilization,
                PercentFormatter.Format(window.Utilization),
                time,
                BarLevelResolver.Resolve(window.Utilization, a.WarnThreshold, a.CritThreshold)));
        }

        Add(r.ShowFiveHour, "5h", snapshot?.FiveHour);
        Add(r.ShowSevenDay, "7d", snapshot?.SevenDay);
        Add(r.ShowSevenDayOpus, "7d Opus", snapshot?.SevenDayOpus);
        Add(r.ShowSevenDaySonnet, "7d Sonnet", snapshot?.SevenDaySonnet);

        var expired = state.Status == UsageStatus.Expired;
        if (expired && rows.Count > 0)
            rows[0] = rows[0] with { TimeText = RunClaudeHint };

        return new WidgetModel(rows, expired, state.Status == UsageStatus.Stale, rows.Count == 0 ? NoRowsNotice : null);
    }
}
```

`src/ClaudeToolbar.Core/Widget/FlyoutModel.cs`:
```csharp
using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Usage;

namespace ClaudeToolbar.Core.Widget;

public sealed record FlyoutModel(IReadOnlyList<string> Lines, string StatusText);

public static class AgoFormatter
{
    public static string Format(TimeSpan ago)
    {
        if (ago < TimeSpan.Zero) ago = TimeSpan.Zero;
        if (ago < TimeSpan.FromMinutes(1)) return $"{(int)ago.TotalSeconds}s";
        if (ago < TimeSpan.FromHours(1)) return $"{(int)ago.TotalMinutes}m";
        return RemainingTimeFormatter.Format(ago);
    }
}

public static class FlyoutModelBuilder
{
    public static FlyoutModel Build(MonitorState state, DateTimeOffset now, Func<DateTimeOffset, string> formatClock)
    {
        var lines = new List<string>();
        if (state.Snapshot is { } s)
        {
            AddLine(lines, "Session", s.FiveHour, now, formatClock);
            AddLine(lines, "Weekly", s.SevenDay, now, formatClock);
            AddLine(lines, "Weekly Opus", s.SevenDayOpus, now, formatClock);
            AddLine(lines, "Weekly Sonnet", s.SevenDaySonnet, now, formatClock);
        }
        if (state.LastSuccess is { } last)
            lines.Add($"Updated {AgoFormatter.Format(now - last)} ago");

        var status = state.Status switch
        {
            UsageStatus.Ok => "OK",
            UsageStatus.Stale => $"Stale: {state.Message ?? "no connection"}",
            UsageStatus.Expired => "Login expired — run claude",
            UsageStatus.NoCredentials => "Not signed in — run claude",
            _ => "Loading…",
        };
        return new FlyoutModel(lines, status);
    }

    private static void AddLine(List<string> lines, string name, UsageWindow? w, DateTimeOffset now, Func<DateTimeOffset, string> formatClock)
    {
        if (w is null) return;
        var percent = PercentFormatter.Format(w.Utilization);
        if (w.ResetsAt is { } reset)
            lines.Add($"{name} {percent} · resets in {RemainingTimeFormatter.Format(reset, now)} · at {formatClock(reset)}");
        else
            lines.Add($"{name} {percent}");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: all pass (entire Core suite).

- [ ] **Step 5: Commit and push**

```bash
git add -A
git commit -m "Add placement math, widget model and flyout model builders"
git push origin main
```

---

### Task 10: App scaffold — WPF project, logging, single instance, startup registration, tray icon

**Files:**
- Create: `src/ClaudeToolbar.App/ClaudeToolbar.App.csproj`, `app.manifest`, `App.xaml`, `App.xaml.cs`
- Create: `src/ClaudeToolbar.App/Services/Log.cs`, `SingleInstance.cs`, `StartupRegistration.cs`
- Create: `src/ClaudeToolbar.App/Tray/IconLoader.cs`, `TrayIcon.cs`, `AppMenu.cs`
- Create: `tools/make-icon.ps1`, `tools/screenshot-taskbar.ps1`, `src/ClaudeToolbar.App/Assets/app.ico` (generated)
- Modify: `.github/workflows/build.yml` (add publish + artifact)

**Interfaces:**
- Consumes: `SettingsStore`, `AppSettings` (Task 6).
- Produces: `Log.Info/Error`, `Log.FilePath`; `SingleInstance.Acquire(Action onOpenSettingsRequested)` with `IsFirst`; `StartupRegistration.Apply(bool)`, `IsEnabled()`; `TrayIcon(string tooltip)` with events `MenuRequested` (right-click), `SettingsRequested` (double-click) and method `SetTooltip(string)`; `AppMenu(bool runAtStartup)` — one WPF context menu shared by the tray icon and (from Task 14) the widget — with events `RefreshRequested`, `SettingsRequested`, `ExitRequested`, `RunAtStartupToggled(bool)` and methods `Show()`, `SetRunAtStartup(bool)`; `IconLoader.LoadAppIcon(int size)`; `App` (partial) with `Settings`, `SettingsStore`, `OpenSettings()`, `SaveSettingsDebounced()` (the last two are filled in by Tasks 14–15; here they log).

- [ ] **Step 1: Generate the icon and the screenshot helper**

`tools/make-icon.ps1` (PowerShell):
```powershell
param([string]$Out = (Join-Path $PSScriptRoot "..\src\ClaudeToolbar.App\Assets\app.ico"))
Add-Type -AssemblyName System.Drawing
$Out = [System.IO.Path]::GetFullPath($Out)
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null

$sizes = 16, 24, 32, 48, 256
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $r = [Math]::Max(2, [int]($s * 0.22)); $d = $r * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($s - 1 - $d, 0, $d, $d, 270, 90)
    $path.AddArc($s - 1 - $d, $s - 1 - $d, $d, $d, 0, 90)
    $path.AddArc(0, $s - 1 - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 217, 119, 87))
    $g.FillPath($bg, $path)

    $fg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $pad = [int]($s * 0.22); $h = [Math]::Max(1, [int]($s * 0.12)); $gap = [Math]::Max(1, [int]($s * 0.14))
    $y1 = [int]($s * 0.30); $y2 = $y1 + $h + $gap
    $g.FillRectangle($fg, $pad, $y1, $s - 2 * $pad, $h)
    $g.FillRectangle($fg, $pad, $y2, [int](($s - 2 * $pad) * 0.6), $h)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , ($ms.ToArray())
    $bmp.Dispose()
}

$fs = [System.IO.File]::Create($Out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush(); $fs.Close()
"wrote $Out"
```

`tools/screenshot-taskbar.ps1` (PowerShell) — captures the bottom-right corner of the primary screen so an agent can look at the widget with the Read tool:
```powershell
param([string]$Out = "$env:TEMP\taskbar.png", [int]$Width = 1000, [int]$Height = 120)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System.Runtime.InteropServices;
public static class DpiHelper { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }
"@
[DpiHelper]::SetProcessDPIAware() | Out-Null
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$w = [Math]::Min($Width, $b.Width); $h = [Math]::Min($Height, $b.Height)
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Right - $w, $b.Bottom - $h, 0, 0, $bmp.Size)
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
"saved $Out ($w x $h from primary screen bottom-right)"
```

Run (PowerShell): `powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1`
Expected: `wrote ...\Assets\app.ico`, file size roughly 3–6 KB.

- [ ] **Step 2: Write the project files**

`src/ClaudeToolbar.App/ClaudeToolbar.App.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <PlatformTarget>x64</PlatformTarget>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>false</SelfContained>
    <AssemblyName>ClaudeToolbar</AssemblyName>
    <RootNamespace>ClaudeToolbar.App</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <ApplicationIcon>Assets\app.ico</ApplicationIcon>
    <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
    <EnableDefaultApplicationDefinition>true</EnableDefaultApplicationDefinition>
  </PropertyGroup>
  <ItemGroup>
    <Using Remove="System.Windows.Forms" />
    <Using Remove="System.Drawing" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\ClaudeToolbar.Core\ClaudeToolbar.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Resource Include="Assets\app.ico" />
  </ItemGroup>
</Project>
```

`src/ClaudeToolbar.App/app.manifest`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="ClaudeToolbar.app"/>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
</assembly>
```

`src/ClaudeToolbar.App/App.xaml`:
```xml
<Application x:Class="ClaudeToolbar.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
        <ResourceDictionary />
    </Application.Resources>
</Application>
```

Add to the solution:
```bash
dotnet sln add src/ClaudeToolbar.App
```

- [ ] **Step 3: Services**

`src/ClaudeToolbar.App/Services/Log.cs`:
```csharp
namespace ClaudeToolbar.App.Services;

public static class Log
{
    private const long MaxBytes = 1_000_000;
    private static readonly object Gate = new();

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeToolbar", "logs");

    public static string FilePath => Path.Combine(LogDirectory, "app.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                    File.Move(FilePath, FilePath + ".1", overwrite: true);
                File.AppendAllText(FilePath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
```

`src/ClaudeToolbar.App/Services/SingleInstance.cs`:
```csharp
namespace ClaudeToolbar.App.Services;

/// <summary>First instance owns a named mutex and listens on a named event; later instances signal it and exit.</summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = "ClaudeToolbar.Instance";
    private const string EventName = "ClaudeToolbar.OpenSettings";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _event;
    private readonly RegisteredWaitHandle? _wait;

    private SingleInstance(Mutex mutex, EventWaitHandle evt, bool isFirst, Action? onSignal)
    {
        _mutex = mutex;
        _event = evt;
        IsFirst = isFirst;
        if (isFirst && onSignal is not null)
            _wait = ThreadPool.RegisterWaitForSingleObject(evt, (_, _) => onSignal(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public bool IsFirst { get; }

    public static SingleInstance Acquire(Action onOpenSettingsRequested)
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        var evt = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        if (!createdNew)
        {
            evt.Set();
            return new SingleInstance(mutex, evt, isFirst: false, onSignal: null);
        }
        return new SingleInstance(mutex, evt, isFirst: true, onOpenSettingsRequested);
    }

    public void Dispose()
    {
        _wait?.Unregister(null);
        if (IsFirst)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _mutex.Dispose();
        _event.Dispose();
    }
}
```

`src/ClaudeToolbar.App/Services/StartupRegistration.cs`:
```csharp
using Microsoft.Win32;

namespace ClaudeToolbar.App.Services;

public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClaudeToolbar";

    public static string ExePath => Environment.ProcessPath ?? throw new InvalidOperationException("Process path unknown");

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
            key.SetValue(ValueName, $"\"{ExePath}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }
}
```

- [ ] **Step 4: Tray icon**

`src/ClaudeToolbar.App/Tray/IconLoader.cs`:
```csharp
using System.Windows;

namespace ClaudeToolbar.App.Tray;

public static class IconLoader
{
    public static System.Drawing.Icon LoadAppIcon(int size)
    {
        var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))
                   ?? throw new InvalidOperationException("app.ico resource missing");
        using var stream = info.Stream;
        return new System.Drawing.Icon(stream, size, size);
    }
}
```

`src/ClaudeToolbar.App/Tray/TrayIcon.cs` — WinForms `NotifyIcon` only for the icon itself; its menu is the WPF `AppMenu` below (a WinForms `ContextMenuStrip` does not close reliably inside a WPF message loop):
```csharp
using WinForms = System.Windows.Forms;

namespace ClaudeToolbar.App.Tray;

public sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;

    public event Action? MenuRequested;
    public event Action? SettingsRequested;

    public TrayIcon(string tooltip)
    {
        _icon = new WinForms.NotifyIcon
        {
            Icon = IconLoader.LoadAppIcon(WinForms.SystemInformation.SmallIconSize.Width),
            Text = Truncate(tooltip),
            Visible = true,
        };
        _icon.MouseUp += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Right) MenuRequested?.Invoke();
        };
        _icon.DoubleClick += (_, _) => SettingsRequested?.Invoke();
    }

    public void SetTooltip(string text) => _icon.Text = Truncate(text);

    private static string Truncate(string text) => text.Length > 63 ? text[..63] : text;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
```

`src/ClaudeToolbar.App/Tray/AppMenu.cs` — a WPF `ContextMenu` opened at the mouse. A popup only auto-closes on outside clicks when its process is in the foreground, so a tiny invisible window is activated first; it hides again when the menu closes.
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ClaudeToolbar.App.Tray;

public sealed class AppMenu : IDisposable
{
    private readonly ContextMenu _menu = new();
    private readonly MenuItem _startupItem;
    private readonly Window _host;

    public event Action? RefreshRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;
    public event Action<bool>? RunAtStartupToggled;

    public AppMenu(bool runAtStartup)
    {
        _host = new Window
        {
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = true,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Opacity = 0,
            Width = 1,
            Height = 1,
            Left = -10000,
            Top = -10000,
            ResizeMode = ResizeMode.NoResize,
        };

        _menu.Items.Add(Item("Refresh now", () => RefreshRequested?.Invoke()));
        _menu.Items.Add(Item("Settings…", () => SettingsRequested?.Invoke()));
        _startupItem = new MenuItem { Header = "Run at startup", IsCheckable = true, IsChecked = runAtStartup };
        _startupItem.Click += (_, _) => RunAtStartupToggled?.Invoke(_startupItem.IsChecked);
        _menu.Items.Add(_startupItem);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(Item("Exit", () => ExitRequested?.Invoke()));
        _menu.Closed += (_, _) => _host.Hide();
    }

    public void Show()
    {
        _host.Show();
        _host.Activate();
        _menu.Placement = PlacementMode.MousePoint;
        _menu.IsOpen = true;
    }

    public void SetRunAtStartup(bool enabled)
    {
        if (_startupItem.IsChecked != enabled) _startupItem.IsChecked = enabled;
    }

    private static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    public void Dispose()
    {
        _menu.IsOpen = false;
        _host.Close();
    }
}
```

- [ ] **Step 5: App startup**

`src/ClaudeToolbar.App/App.xaml.cs`:
```csharp
using System.Windows;
using System.Windows.Threading;
using ClaudeToolbar.App.Services;
using ClaudeToolbar.App.Tray;
using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.App;

public partial class App : Application
{
    private SingleInstance? _instance;
    private TrayIcon? _tray;
    private AppMenu? _menu;
    private DispatcherTimer? _saveTimer;

    public AppSettings Settings { get; private set; } = AppSettings.CreateDefault();
    public SettingsStore SettingsStore { get; private set; } = new(SettingsStore.DefaultPath());

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) => { Log.Error("Unhandled UI exception", args.Exception); args.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Error("Unhandled exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => { Log.Error("Unobserved task exception", args.Exception); args.SetObserved(); };

        _instance = SingleInstance.Acquire(() => Dispatcher.InvokeAsync(OpenSettings));
        if (!_instance.IsFirst)
        {
            Log.Info("Another instance is running; asked it to open settings and exiting");
            Shutdown();
            return;
        }

        Settings = SettingsStore.Load();
        if (!File.Exists(SettingsStore.Path)) SettingsStore.Save(Settings);
        TrySafe(() => StartupRegistration.Apply(Settings.Behavior.RunAtStartup), "startup registration");

        _menu = new AppMenu(Settings.Behavior.RunAtStartup);
        _menu.ExitRequested += Shutdown;
        _menu.SettingsRequested += OpenSettings;
        _menu.RefreshRequested += RefreshNow;
        _menu.RunAtStartupToggled += on =>
        {
            Settings.Behavior.RunAtStartup = on;
            TrySafe(() => StartupRegistration.Apply(on), "startup registration");
            SaveSettingsDebounced();
        };

        _tray = new TrayIcon("Claude Toolbar");
        _tray.MenuRequested += _menu.Show;
        _tray.SettingsRequested += OpenSettings;

        Log.Info("Started");
        OnStartupCore(e);
    }

    /// <summary>Extended by later tasks (widget + monitor wiring).</summary>
    partial void OnStartupCore(StartupEventArgs e);

    public void OpenSettings() => OpenSettingsCore();
    partial void OpenSettingsCore();

    public void RefreshNow() => RefreshNowCore();
    partial void RefreshNowCore();

    public TrayIcon? Tray => _tray;

    public void SaveSettingsDebounced()
    {
        _saveTimer ??= new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(300) };
        _saveTimer.Stop();
        _saveTimer.Tick -= SaveTick;
        _saveTimer.Tick += SaveTick;
        _saveTimer.Start();
    }

    private void SaveTick(object? sender, EventArgs e)
    {
        _saveTimer!.Stop();
        TrySafe(() => SettingsStore.Save(Settings), "save settings");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _saveTimer?.Stop();
        OnExitCore();
        _tray?.Dispose();
        _menu?.Dispose();
        _instance?.Dispose();
        Log.Info("Exited");
        base.OnExit(e);
    }

    partial void OnExitCore();

    public static void TrySafe(Action action, string what)
    {
        try { action(); }
        catch (Exception ex) { Log.Error($"Failed: {what}", ex); }
    }
}
```

`partial void` methods without a body are legal and compile to nothing; Tasks 14 and 15 add `App.Widget.cs` / `App.Settings.cs` partial files that implement them. In this task also add a tiny placeholder file so intent is clear:

`src/ClaudeToolbar.App/App.Placeholders.cs`:
```csharp
using System.Windows;
using ClaudeToolbar.App.Services;

namespace ClaudeToolbar.App;

public partial class App
{
    partial void OpenSettingsCore() => Log.Info("Settings requested (window not implemented yet)");
    partial void RefreshNowCore() => Log.Info("Refresh requested (monitor not wired yet)");
}
```
(Task 14 deletes `RefreshNowCore` from this file; Task 15 deletes the file entirely.)

- [ ] **Step 6: Build and verify manually**

```bash
dotnet build src/ClaudeToolbar.App
```
Expected: build succeeded, 0 warnings.

PowerShell:
```powershell
Start-Process "src\ClaudeToolbar.App\bin\Debug\net10.0-windows\win-x64\ClaudeToolbar.exe"
Start-Sleep 3
Get-Process ClaudeToolbar | Select-Object Id, ProcessName
Get-Content "$env:LOCALAPPDATA\ClaudeToolbar\logs\app.log" -Tail 5
Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" | Select-Object ClaudeToolbar
Start-Process "src\ClaudeToolbar.App\bin\Debug\net10.0-windows\win-x64\ClaudeToolbar.exe"; Start-Sleep 2
Get-Content "$env:LOCALAPPDATA\ClaudeToolbar\logs\app.log" -Tail 3
Test-Path "$env:APPDATA\ClaudeToolbar\settings.json"
Stop-Process -Name ClaudeToolbar
```
Expected: one process; log shows `Started`; Run value points at the exe; second launch logs "Another instance…" and "Settings requested"; settings.json exists. If a human is at the machine: right-click the tray icon → a four-item menu appears and closes when clicking elsewhere; Exit quits the app. Take `powershell -ExecutionPolicy Bypass -File tools/screenshot-taskbar.ps1` before stopping and Read the PNG: an orange icon should be visible in the tray (it may be inside the hidden-icons overflow; that is fine).

- [ ] **Step 7: Extend CI with publish**

Append these steps to the `build` job in `.github/workflows/build.yml`:
```yaml
      - run: dotnet publish src/ClaudeToolbar.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
      - uses: actions/upload-artifact@v4
        with:
          name: ClaudeToolbar-win-x64
          path: publish/ClaudeToolbar.exe
```

- [ ] **Step 8: Commit and push**

```bash
git add -A
git commit -m "Add WPF app shell with tray icon, single instance and startup registration"
git push origin main
```

---

### Task 11: Win32 interop and taskbar discovery

**Files:**
- Create: `src/ClaudeToolbar.App/Interop/NativeMethods.cs`, `TaskbarLayout.cs`, `TaskbarLocator.cs`
- Modify: `src/ClaudeToolbar.App/App.xaml.cs` (handle `--dump-taskbar`)

**Interfaces:**
- Consumes: `RectI` (Task 9).
- Produces: `NativeMethods` (P/Invoke + constants listed below); `record TaskbarLayout(IntPtr TrayHwnd, IntPtr NotifyHwnd, RectI Taskbar, RectI TaskbarNow, RectI Notify, RectI Monitor, bool AutoHide, uint ExplorerPid)`; `TaskbarLocator.Locate() : TaskbarLayout?`, `TaskbarLocator.Refresh(TaskbarLayout previous) : TaskbarLayout?`.

- [ ] **Step 1: NativeMethods**

`src/ClaudeToolbar.App/Interop/NativeMethods.cs`:
```csharp
using System.Runtime.InteropServices;

namespace ClaudeToolbar.App.Interop;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    public const uint ABM_GETSTATE = 0x4;
    public const uint ABM_GETTASKBARPOS = 0x5;
    public const int ABS_AUTOHIDE = 0x1;

    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_TOPMOST = 0x8;
    public const long WS_EX_TOOLWINDOW = 0x80;
    public const long WS_EX_NOACTIVATE = 0x08000000;

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOSIZE = 0x1;
    public const uint SWP_NOMOVE = 0x2;
    public const uint SWP_NOACTIVATE = 0x10;
    public const uint SWP_NOOWNERZORDER = 0x200;

    public const int WM_SETTINGCHANGE = 0x1A;
    public const int WM_MOUSEACTIVATE = 0x21;
    public const int WM_DISPLAYCHANGE = 0x7E;
    public const int WM_DPICHANGED = 0x02E0;
    public const int MA_NOACTIVATE = 3;

    public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    public const uint WINEVENT_OUTOFCONTEXT = 0;
    public const int OBJID_WINDOW = 0;

    public const uint GW_HWNDNEXT = 2;
    public const uint MONITOR_DEFAULTTONEAREST = 2;

    public const int QUNS_BUSY = 2;
    public const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
    public const int QUNS_PRESENTATION_MODE = 4;

    public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetTopWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("shell32.dll")]
    public static extern UIntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [DllImport("shell32.dll")]
    public static extern int SHQueryUserNotificationState(out int pquns);
}
```

- [ ] **Step 2: Layout record and locator**

`src/ClaudeToolbar.App/Interop/TaskbarLayout.cs`:
```csharp
using ClaudeToolbar.Core.Layout;

namespace ClaudeToolbar.App.Interop;

/// <summary>Snapshot of where the primary taskbar and its notification area are, in physical pixels.</summary>
/// <param name="Taskbar">Docked rect from the AppBar API (where the taskbar lives when shown).</param>
/// <param name="TaskbarNow">Current window rect of Shell_TrayWnd (moves during auto-hide animation).</param>
/// <param name="Notify">Current rect of TrayNotifyWnd.</param>
public sealed record TaskbarLayout(
    IntPtr TrayHwnd,
    IntPtr NotifyHwnd,
    RectI Taskbar,
    RectI TaskbarNow,
    RectI Notify,
    RectI Monitor,
    bool AutoHide,
    uint ExplorerPid);
```

`src/ClaudeToolbar.App/Interop/TaskbarLocator.cs`:
```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using ClaudeToolbar.Core.Layout;
using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Interop;

public static class TaskbarLocator
{
    public static TaskbarLayout? Locate()
    {
        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero) return null;

        GetWindowThreadProcessId(tray, out var pid);
        if (!IsExplorer(pid)) return null;

        var notify = FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
        if (notify == IntPtr.Zero) return null;

        return ReadRects(tray, notify, pid);
    }

    /// <summary>Re-reads rects for a previously located taskbar. Returns null if its windows are gone.</summary>
    public static TaskbarLayout? Refresh(TaskbarLayout previous)
    {
        if (!IsWindow(previous.TrayHwnd) || !IsWindow(previous.NotifyHwnd)) return null;
        return ReadRects(previous.TrayHwnd, previous.NotifyHwnd, previous.ExplorerPid);
    }

    private static TaskbarLayout? ReadRects(IntPtr tray, IntPtr notify, uint pid)
    {
        if (!GetWindowRect(tray, out var trayNow) || !GetWindowRect(notify, out var notifyRect)) return null;

        var pos = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
        var hasPos = SHAppBarMessage(ABM_GETTASKBARPOS, ref pos) != UIntPtr.Zero;
        var taskbar = hasPos ? ToRect(pos.rc) : ToRect(trayNow);

        var state = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
        var autoHide = (SHAppBarMessage(ABM_GETSTATE, ref state).ToUInt32() & ABS_AUTOHIDE) != 0;

        var monitorHandle = MonitorFromWindow(tray, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        var monitor = GetMonitorInfo(monitorHandle, ref info) ? ToRect(info.rcMonitor) : taskbar;

        return new TaskbarLayout(tray, notify, taskbar, ToRect(trayNow), ToRect(notifyRect), monitor, autoHide, pid);
    }

    private static RectI ToRect(RECT r) => new(r.Left, r.Top, r.Right, r.Bottom);

    private static bool IsExplorer(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return string.Equals(process.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}
```

- [ ] **Step 3: Diagnostic switch**

In `App.xaml.cs`, at the top of `OnStartup` right after `base.OnStartup(e);` insert:
```csharp
        if (e.Args.Contains("--dump-taskbar", StringComparer.OrdinalIgnoreCase))
        {
            DumpTaskbar();
            Shutdown();
            return;
        }
```
and add this method to the class:
```csharp
    private static void DumpTaskbar()
    {
        var layout = Interop.TaskbarLocator.Locate();
        var text = layout is null
            ? "Taskbar not found"
            : $"Tray HWND: 0x{layout.TrayHwnd:X}\nNotify HWND: 0x{layout.NotifyHwnd:X}\nTaskbar (docked): {layout.Taskbar}\nTaskbar (now): {layout.TaskbarNow}\nNotify: {layout.Notify}\nMonitor: {layout.Monitor}\nAutoHide: {layout.AutoHide}\nExplorer PID: {layout.ExplorerPid}";
        var path = Path.Combine(Log.LogDirectory, "taskbar-dump.txt");
        Directory.CreateDirectory(Log.LogDirectory);
        File.WriteAllText(path, text);
        Log.Info($"Wrote taskbar dump to {path}");
    }
```

- [ ] **Step 4: Build and verify**

```bash
dotnet build src/ClaudeToolbar.App
```
PowerShell:
```powershell
& "src\ClaudeToolbar.App\bin\Debug\net10.0-windows\win-x64\ClaudeToolbar.exe" --dump-taskbar
Start-Sleep 2
Get-Content "$env:LOCALAPPDATA\ClaudeToolbar\logs\taskbar-dump.txt"
```
Expected: rects printed; `Notify.Left` is a few hundred px left of the screen's right edge; `TaskbarNow.Height` is about 48 at 100 % scale (larger at higher DPI); `AutoHide: False` unless the user enabled it.

- [ ] **Step 5: Commit and push**

```bash
git add -A
git commit -m "Add Win32 interop and taskbar discovery"
git push origin main
```

---

### Task 12: Widget window and row rendering

**Files:**
- Create: `src/ClaudeToolbar.App/Widget/WidgetTheme.cs`, `UsageRowsControl.cs`, `WidgetWindow.xaml`, `WidgetWindow.xaml.cs`
- Create: `src/ClaudeToolbar.App/App.Widget.cs` (temporary sample wiring; Task 14 replaces its body)

**Interfaces:**
- Consumes: `WidgetModel`, `WidgetRow` (Task 9), `AppearanceSettings`, `RowSettings` (Task 6), `BarLevel` (Task 2), `TaskbarLocator`, `NativeMethods` (Task 11), `WidgetPlacement`, `RectI` (Task 9).
- Produces: `WidgetTheme.FromSettings(AppearanceSettings)` with brushes `Background, Text, BarTrack, BarOk, BarWarn, BarCrit`, `FontSize`, `CornerRadius`, `BrushFor(BarLevel)`; `UsageRowsControl.Render(WidgetModel, RowSettings, WidgetTheme)` and `UpdateTimes(WidgetModel)`; `WidgetWindow` with `Handle`, `event Action<int>? ShellMessage`, `Render(...)`, `UpdateTimes(...)`, `PhysicalSize()`, `CurrentRect()`, `MoveTo(RectI)`, `AssertTopmost()`, `SetMaxPhysicalHeight(int)`, `ShowNoActivate()`, `HideWidget()`, `IsShown`, `Root` grid.

- [ ] **Step 1: Theme and rows control**

`src/ClaudeToolbar.App/Widget/WidgetTheme.cs`:
```csharp
using System.Windows.Media;
using ClaudeToolbar.Core.Formatting;
using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.App.Widget;

public sealed class WidgetTheme
{
    public required Brush Background { get; init; }
    public required Brush Text { get; init; }
    public required Brush BarTrack { get; init; }
    public required Brush BarOk { get; init; }
    public required Brush BarWarn { get; init; }
    public required Brush BarCrit { get; init; }
    public required double FontSize { get; init; }
    public required double CornerRadius { get; init; }

    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");

    public static WidgetTheme FromSettings(AppearanceSettings a) => new()
    {
        Background = BrushFrom(a.Background),
        Text = BrushFrom(a.Text),
        BarTrack = BrushFrom(a.BarTrack),
        BarOk = BrushFrom(a.BarOk),
        BarWarn = BrushFrom(a.BarWarn),
        BarCrit = BrushFrom(a.BarCrit),
        FontSize = a.FontSize,
        CornerRadius = a.CornerRadius,
    };

    public Brush BrushFor(BarLevel level) => level switch
    {
        BarLevel.Ok => BarOk,
        BarLevel.Warn => BarWarn,
        _ => BarCrit,
    };

    public static SolidColorBrush BrushFrom(string argb)
    {
        var color = (Color)ColorConverter.ConvertFromString(argb);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
```

`src/ClaudeToolbar.App/Widget/UsageRowsControl.cs`:
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.App.Widget;

/// <summary>Draws the stacked usage rows. Imperative rendering keeps the widget and the settings preview identical.</summary>
public sealed class UsageRowsControl : Border
{
    private const double BarHeight = 4;
    private const double Gap = 6;

    private readonly StackPanel _rows = new() { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
    private readonly Ellipse _staleDot = new() { Width = 6, Height = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Gap, 0, 0, 0), Visibility = Visibility.Collapsed };
    private readonly List<(TextBlock Time, TextBlock Percent)> _live = new();

    public UsageRowsControl()
    {
        var panel = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_staleDot, Dock.Right);
        panel.Children.Add(_staleDot);
        panel.Children.Add(_rows);
        Child = panel;
        Padding = new Thickness(8, 2, 8, 2);
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
    }

    public void Render(WidgetModel model, RowSettings rows, WidgetTheme theme)
    {
        Background = theme.Background;
        CornerRadius = new CornerRadius(theme.CornerRadius);
        Opacity = model.Dimmed ? 0.5 : 1.0;
        _staleDot.Fill = theme.BarWarn;
        _staleDot.Visibility = model.ShowStaleDot ? Visibility.Visible : Visibility.Collapsed;

        _rows.Children.Clear();
        _live.Clear();

        if (model.Rows.Count == 0)
        {
            _rows.Children.Add(MakeText(model.Notice ?? string.Empty, theme, 0));
            return;
        }

        foreach (var row in model.Rows)
            _rows.Children.Add(MakeRow(row, rows, theme));
    }

    public void UpdateTimes(WidgetModel model)
    {
        for (var i = 0; i < _live.Count && i < model.Rows.Count; i++)
        {
            var (time, percent) = _live[i];
            if (time.Text != model.Rows[i].TimeText) time.Text = model.Rows[i].TimeText;
            if (percent.Text != model.Rows[i].PercentText) percent.Text = model.Rows[i].PercentText;
        }
    }

    private FrameworkElement MakeRow(WidgetRow row, RowSettings rows, WidgetTheme theme)
    {
        var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        for (var i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = MakeText(row.Label, theme, 0);
        label.MinWidth = 18;
        label.Visibility = rows.ShowLabel ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var track = new Border
        {
            Width = rows.BarWidth,
            Height = BarHeight,
            CornerRadius = new CornerRadius(BarHeight / 2),
            Background = theme.BarTrack,
            Margin = new Thickness(Gap, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = rows.ShowBar ? Visibility.Visible : Visibility.Collapsed,
            Child = new Border
            {
                Width = rows.BarWidth * Math.Clamp(row.Utilization, 0, 100) / 100.0,
                Height = BarHeight,
                CornerRadius = new CornerRadius(BarHeight / 2),
                Background = theme.BrushFor(row.Level),
                HorizontalAlignment = HorizontalAlignment.Left,
            },
        };
        Grid.SetColumn(track, 1);
        grid.Children.Add(track);

        var percent = MakeText(row.PercentText, theme, Gap);
        percent.MinWidth = 30;
        percent.TextAlignment = TextAlignment.Right;
        percent.Visibility = rows.ShowPercent ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(percent, 2);
        grid.Children.Add(percent);

        var time = MakeText(row.TimeText, theme, Gap);
        time.MinWidth = 40;
        time.Visibility = rows.ShowTime ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(time, 3);
        grid.Children.Add(time);

        _live.Add((time, percent));
        return grid;
    }

    private static TextBlock MakeText(string text, WidgetTheme theme, double leftMargin) => new()
    {
        Text = text,
        Foreground = theme.Text,
        FontFamily = WidgetTheme.Font,
        FontSize = theme.FontSize,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(leftMargin, 0, 0, 0),
    };
}
```

- [ ] **Step 2: Widget window**

`src/ClaudeToolbar.App/Widget/WidgetWindow.xaml`:
```xml
<Window x:Class="ClaudeToolbar.App.Widget.WidgetWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Claude Toolbar"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        ShowInTaskbar="False"
        ShowActivated="False"
        Topmost="True"
        ResizeMode="NoResize"
        SizeToContent="WidthAndHeight"
        WindowStartupLocation="Manual"
        Left="-10000" Top="-10000">
    <Grid x:Name="Root" Background="Transparent" />
</Window>
```

`src/ClaudeToolbar.App/Widget/WidgetWindow.xaml.cs`:
```csharp
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ClaudeToolbar.Core.Layout;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Widget;
using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Widget;

public partial class WidgetWindow : Window
{
    private readonly UsageRowsControl _rows = new();
    private HwndSource? _source;

    public WidgetWindow()
    {
        InitializeComponent();
        Root.Children.Add(_rows);
    }

    public IntPtr Handle { get; private set; }

    public bool IsShown { get; private set; }

    /// <summary>Raw window messages (msg id) for the taskbar tracker.</summary>
    public event Action<int>? ShellMessage;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Handle = new WindowInteropHelper(this).Handle;
        var ex = GetWindowLongPtr(Handle, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(Handle, GWL_EXSTYLE, new IntPtr(ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST));
        _source = HwndSource.FromHwnd(Handle);
        _source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(MA_NOACTIVATE);
        }
        ShellMessage?.Invoke(msg);
        return IntPtr.Zero;
    }

    public void Render(WidgetModel model, RowSettings rows, WidgetTheme theme) => _rows.Render(model, rows, theme);

    public void UpdateTimes(WidgetModel model) => _rows.UpdateTimes(model);

    public (int Width, int Height) PhysicalSize()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return ((int)Math.Round(ActualWidth * dpi.DpiScaleX), (int)Math.Round(ActualHeight * dpi.DpiScaleY));
    }

    public void SetMaxPhysicalHeight(int physical)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var logical = physical / dpi.DpiScaleY;
        if (Math.Abs(MaxHeight - logical) > 0.5) MaxHeight = logical;
    }

    public RectI CurrentRect()
    {
        if (Handle == IntPtr.Zero || !GetWindowRect(Handle, out var r)) return default;
        return new RectI(r.Left, r.Top, r.Right, r.Bottom);
    }

    public void MoveTo(RectI target)
    {
        if (Handle == IntPtr.Zero) return;
        SetWindowPos(Handle, HWND_TOPMOST, target.Left, target.Top, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    public void AssertTopmost()
    {
        if (Handle == IntPtr.Zero) return;
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    public void ShowNoActivate()
    {
        if (IsShown) return;
        Show();
        IsShown = true;
    }

    public void HideWidget()
    {
        if (!IsShown) return;
        Hide();
        IsShown = false;
    }
}
```

- [ ] **Step 3: Temporary sample wiring**

`src/ClaudeToolbar.App/App.Widget.cs` (Task 14 rewrites this file completely):
```csharp
using System.Windows;
using ClaudeToolbar.App.Interop;
using ClaudeToolbar.App.Services;
using ClaudeToolbar.App.Widget;
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Layout;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Usage;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.App;

public partial class App
{
    private WidgetWindow? _widget;

    partial void OnStartupCore(StartupEventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new UsageSnapshot(
            new UsageWindow(42, now.AddHours(2).AddMinutes(13)),
            new UsageWindow(18, now.AddDays(3).AddHours(4)),
            null, null, now);
        var state = new MonitorState(UsageStatus.Ok, snapshot, now, null, new CredentialsState.Missing("sample"));
        var model = WidgetModelBuilder.Build(state, Settings, now);

        _widget = new WidgetWindow();
        _widget.Render(model, Settings.Rows, WidgetTheme.FromSettings(Settings.Appearance));
        _widget.ShowNoActivate();

        var layout = TaskbarLocator.Locate();
        if (layout is null)
        {
            Log.Error("Taskbar not found");
            return;
        }
        _widget.SetMaxPhysicalHeight(WidgetPlacement.MaxWidgetHeight(layout.TaskbarNow));
        _widget.UpdateLayout();
        var (w, h) = _widget.PhysicalSize();
        var target = WidgetPlacement.Compute(layout.TaskbarNow, layout.Notify, w, h, Settings.Behavior.TrayGapPx);
        _widget.MoveTo(target);
        Log.Info($"Widget placed at {target} (size {w}x{h})");
    }

    partial void OnExitCore()
    {
        _widget?.Close();
    }
}
```

- [ ] **Step 4: Build and verify visually**

```bash
dotnet build src/ClaudeToolbar.App
```
PowerShell:
```powershell
Start-Process "src\ClaudeToolbar.App\bin\Debug\net10.0-windows\win-x64\ClaudeToolbar.exe"; Start-Sleep 3
powershell -ExecutionPolicy Bypass -File tools/screenshot-taskbar.ps1 -Out "$env:TEMP\widget.png"
Get-Content "$env:LOCALAPPDATA\ClaudeToolbar\logs\app.log" -Tail 3
```
Read `%TEMP%\widget.png` with the Read tool. Expected: two rows (`5h ▬▬ 42% 2h 13m`, `7d ▬ 18% 3d 4h`) inside a rounded dark pill, sitting immediately left of the tray icons, vertically centred in the taskbar, not overlapping the chevron. Then click on the desktop and take another screenshot: the widget must still be visible (topmost) and must not have taken focus (the log has no exception; the previously focused window stays focused). Finally `Stop-Process -Name ClaudeToolbar`.

If the widget renders but is positioned wrong, compare with `--dump-taskbar` numbers before changing code.

- [ ] **Step 5: Commit and push**

```bash
git add -A
git commit -m "Add taskbar widget window with row rendering"
git push origin main
```

---

### Task 13: Taskbar tracker — follow the taskbar, hide in fullscreen, stay on top

**Files:**
- Create: `src/ClaudeToolbar.App/Interop/WinEventHook.cs`, `ShellState.cs`, `TaskbarTracker.cs`
- Create: `src/ClaudeToolbar.App/Widget/WidgetController.cs`
- Modify: `src/ClaudeToolbar.App/App.Widget.cs` (use the controller with sample data)

**Interfaces:**
- Consumes: `WidgetWindow` (Task 12), `TaskbarLocator`, `TaskbarLayout`, `NativeMethods` (Task 11), `WidgetPlacement` (Task 9), `AppSettings`.
- Produces: `WinEventHook(uint pid, Func<IntPtr,bool> filter, Action onEvent)`; `ShellState.IsFullscreenAppActive()`, `ShellState.IsAbove(IntPtr a, IntPtr b)`; `TaskbarTracker(WidgetWindow)` with `Layout`, `event Action? Changed`, `Start()`, `Relocate()`, `Evaluate(bool force)`, `Dispose()`; `WidgetController(WidgetWindow, TaskbarTracker, Func<AppSettings>)` with `Start()`, `Relocate()`, `Reposition()`, `Dispose()`.

- [ ] **Step 1: Hook and shell state**

`src/ClaudeToolbar.App/Interop/WinEventHook.cs`:
```csharp
using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Interop;

/// <summary>Out-of-context location-change hook scoped to one process. Callbacks arrive on the creating (UI) thread.</summary>
public sealed class WinEventHook : IDisposable
{
    private readonly WinEventDelegate _callback;
    private readonly Func<IntPtr, bool> _filter;
    private readonly Action _onEvent;
    private IntPtr _hook;

    public WinEventHook(uint pid, Func<IntPtr, bool> filter, Action onEvent)
    {
        _filter = filter;
        _onEvent = onEvent;
        _callback = Callback;
        _hook = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _callback, pid, 0, WINEVENT_OUTOFCONTEXT);
    }

    private void Callback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (idObject == OBJID_WINDOW && _filter(hwnd)) _onEvent();
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
```

`src/ClaudeToolbar.App/Interop/ShellState.cs`:
```csharp
using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Interop;

public static class ShellState
{
    public static bool IsFullscreenAppActive()
    {
        if (SHQueryUserNotificationState(out var state) != 0) return false;
        return state is QUNS_BUSY or QUNS_RUNNING_D3D_FULL_SCREEN or QUNS_PRESENTATION_MODE;
    }

    /// <summary>True when window <paramref name="a"/> is above <paramref name="b"/> in the top-level z-order.</summary>
    public static bool IsAbove(IntPtr a, IntPtr b)
    {
        for (var h = GetTopWindow(IntPtr.Zero); h != IntPtr.Zero; h = GetWindow(h, GW_HWNDNEXT))
        {
            if (h == a) return true;
            if (h == b) return false;
        }
        return false;
    }
}
```

- [ ] **Step 2: Tracker**

`src/ClaudeToolbar.App/Interop/TaskbarTracker.cs`:
```csharp
using System.Windows.Threading;
using ClaudeToolbar.App.Services;
using ClaudeToolbar.App.Widget;
using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Interop;

/// <summary>Keeps an up-to-date <see cref="TaskbarLayout"/> and raises <see cref="Changed"/> on the UI thread whenever it moves.</summary>
public sealed class TaskbarTracker : IDisposable
{
    private static readonly TimeSpan LocateRetry = TimeSpan.FromSeconds(3);

    private readonly WidgetWindow _window;
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private readonly uint _taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");
    private WinEventHook? _hook;
    private DateTime _lastLocateAttempt = DateTime.MinValue;
    private bool _evaluateQueued;

    public TaskbarTracker(WidgetWindow window)
    {
        _window = window;
        _window.ShellMessage += OnShellMessage;
        _timer.Tick += (_, _) => Evaluate(force: false);
    }

    public TaskbarLayout? Layout { get; private set; }

    public event Action? Changed;

    public void Start()
    {
        Relocate();
        _timer.Start();
    }

    private void OnShellMessage(int msg)
    {
        if (msg == (int)_taskbarCreatedMsg || msg is WM_DISPLAYCHANGE or WM_DPICHANGED or WM_SETTINGCHANGE)
            QueueEvaluate(relocate: msg == (int)_taskbarCreatedMsg || msg == WM_DISPLAYCHANGE);
    }

    /// <summary>Forget cached handles and search for the taskbar again.</summary>
    public void Relocate()
    {
        _hook?.Dispose();
        _hook = null;
        _lastLocateAttempt = DateTime.UtcNow;
        Layout = TaskbarLocator.Locate();
        if (Layout is { } l)
        {
            var tray = l.TrayHwnd;
            var notify = l.NotifyHwnd;
            _hook = new WinEventHook(l.ExplorerPid, h => h == tray || h == notify, () => QueueEvaluate(relocate: false));
            Log.Info($"Taskbar located: now={l.TaskbarNow} notify={l.Notify} autohide={l.AutoHide}");
        }
        else
        {
            Log.Info("Taskbar not found; will retry");
        }
        Changed?.Invoke();
    }

    public void Evaluate(bool force)
    {
        if (Layout is null)
        {
            if (DateTime.UtcNow - _lastLocateAttempt >= LocateRetry) Relocate();
            return;
        }

        var fresh = TaskbarLocator.Refresh(Layout);
        if (fresh is null)
        {
            Relocate();
            return;
        }

        if (force || fresh != Layout)
        {
            Layout = fresh;
            Changed?.Invoke();
        }
    }

    private void QueueEvaluate(bool relocate)
    {
        if (_evaluateQueued) return;
        _evaluateQueued = true;
        _window.Dispatcher.InvokeAsync(() =>
        {
            _evaluateQueued = false;
            if (relocate) Relocate();
            else Evaluate(force: true);
        }, DispatcherPriority.Background);
    }

    public void Dispose()
    {
        _timer.Stop();
        _window.ShellMessage -= OnShellMessage;
        _hook?.Dispose();
    }
}
```

- [ ] **Step 3: Controller**

`src/ClaudeToolbar.App/Widget/WidgetController.cs`:
```csharp
using ClaudeToolbar.App.Interop;
using ClaudeToolbar.Core.Layout;
using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.App.Widget;

/// <summary>Applies the tracked taskbar layout to the widget window: show/hide, place, keep on top.</summary>
public sealed class WidgetController : IDisposable
{
    private readonly WidgetWindow _window;
    private readonly TaskbarTracker _tracker;
    private readonly Func<AppSettings> _settings;

    public WidgetController(WidgetWindow window, TaskbarTracker tracker, Func<AppSettings> settings)
    {
        _window = window;
        _tracker = tracker;
        _settings = settings;
        _tracker.Changed += Reposition;
        _window.SizeChanged += (_, _) => Reposition();
    }

    public void Start() => _tracker.Start();

    /// <summary>Forget cached taskbar handles and search again (explorer restart, display change, resume).</summary>
    public void Relocate() => _tracker.Relocate();

    public void Reposition()
    {
        var layout = _tracker.Layout;
        if (layout is null)
        {
            _window.HideWidget();
            return;
        }

        var behavior = _settings().Behavior;
        var fullscreen = behavior.HideInFullscreen && ShellState.IsFullscreenAppActive();
        var taskbarHidden = layout.AutoHide && WidgetPlacement.IsTaskbarMostlyHidden(layout.TaskbarNow, layout.Monitor);
        if (fullscreen || taskbarHidden)
        {
            _window.HideWidget();
            return;
        }

        _window.SetMaxPhysicalHeight(WidgetPlacement.MaxWidgetHeight(layout.TaskbarNow));
        _window.ShowNoActivate();
        _window.UpdateLayout();

        var (w, h) = _window.PhysicalSize();
        if (w == 0 || h == 0) return;

        var target = WidgetPlacement.Compute(layout.TaskbarNow, layout.Notify, w, h, behavior.TrayGapPx);
        if (target != _window.CurrentRect())
            _window.MoveTo(target);
        else if (ShellState.IsAbove(layout.TrayHwnd, _window.Handle))
            _window.AssertTopmost();
    }

    public void Dispose()
    {
        _tracker.Changed -= Reposition;
        _tracker.Dispose();
    }
}
```

- [ ] **Step 4: Use the controller with sample data**

Replace the body of `OnStartupCore` in `App.Widget.cs` so it ends with the controller instead of a one-shot placement, and add fields:
```csharp
    private WidgetWindow? _widget;
    private WidgetController? _controller;

    partial void OnStartupCore(StartupEventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new UsageSnapshot(
            new UsageWindow(42, now.AddHours(2).AddMinutes(13)),
            new UsageWindow(18, now.AddDays(3).AddHours(4)),
            null, null, now);
        var state = new MonitorState(UsageStatus.Ok, snapshot, now, null, new CredentialsState.Missing("sample"));
        var model = WidgetModelBuilder.Build(state, Settings, now);

        _widget = new WidgetWindow();
        _widget.Render(model, Settings.Rows, WidgetTheme.FromSettings(Settings.Appearance));
        _controller = new WidgetController(_widget, new TaskbarTracker(_widget), () => Settings);
        _controller.Start();
    }

    partial void OnExitCore()
    {
        _controller?.Dispose();
        _widget?.Close();
    }
```
Note `ShowNoActivate` must be called before `PhysicalSize` works; the controller does that. Because `WidgetWindow` starts at Left/Top −10000, the first `Reposition` moves it into place before it is ever visible on screen.

- [ ] **Step 5: Build and verify**

```bash
dotnet build src/ClaudeToolbar.App
```
PowerShell, with the app running (`Start-Process ...ClaudeToolbar.exe`), run each check and take `tools/screenshot-taskbar.ps1` after each:
1. Baseline: widget left of tray.
2. Change scale: `Settings > System > Display > Scale` cannot be scripted safely; instead change resolution via `Set-DisplayResolution` if available, otherwise skip and note it in the task report. The `WM_DISPLAYCHANGE` path is also exercised by step 4.
3. Restart explorer: `Stop-Process -Name explorer -Force; Start-Sleep 5` (Windows restarts it). Screenshot: widget re-appears at the right spot. Log shows "Taskbar located" twice.
4. Toggle auto-hide: `Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3"` is fragile; instead open Settings > Personalization > Taskbar > Taskbar behaviors and tick "Automatically hide the taskbar" by hand if a human is available; otherwise skip and note. When hidden, the widget must disappear; when the mouse reveals the taskbar, the widget follows.
5. Fullscreen: `Start-Process "C:\Windows\System32\mspaint.exe"` is not fullscreen; use a browser window and press F11, or run any game. Widget must hide within 1 s and return after leaving fullscreen.
6. Focus: with Notepad focused, hover and click the widget: Notepad keeps focus (title bar stays active).
Report which checks were run. Stop the app with `Stop-Process -Name ClaudeToolbar`.

- [ ] **Step 6: Commit and push**

```bash
git add -A
git commit -m "Track the taskbar and keep the widget placed, visible and on top"
git push origin main
```

---

### Task 14: Live data — monitor wiring, credentials watcher, system events, flyout, widget menu

**Files:**
- Create: `src/ClaudeToolbar.App/Services/CredentialsWatcher.cs`
- Create: `src/ClaudeToolbar.App/Widget/WidgetWindow.Interaction.cs`
- Rewrite: `src/ClaudeToolbar.App/App.Widget.cs`
- Modify: `src/ClaudeToolbar.App/App.Placeholders.cs` (remove `RefreshNowCore`)

**Interfaces:**
- Consumes: `UsageMonitor`, `MonitorState`, `UsageStatus` (Task 8), `FileCredentialsSource`, `CredentialsPaths` (Task 4), `UsageClient` (Task 5), `RefreshScheduler` (Task 7), `WidgetModelBuilder`, `FlyoutModelBuilder` (Task 9), `WidgetWindow`, `WidgetTheme` (Task 12), `WidgetController` (Task 13), `AppMenu`, `TrayIcon` (Task 10).
- Produces: `CredentialsWatcher(string filePath, Action onChanged)`; on `WidgetWindow`: events `Clicked`, `MenuRequested`, `FlyoutRequested`, property `IsFlyoutOpen`, methods `ShowFlyout(FlyoutModel, WidgetTheme)`, `HideFlyout()`; on `App`: `event Action<MonitorState>? MonitorStateChanged` (UI thread), `MonitorState? CurrentState`, `void ApplySettingsLive()`, `RefreshNow()` now actually refreshes.

- [ ] **Step 1: Credentials watcher**

`src/ClaudeToolbar.App/Services/CredentialsWatcher.cs`:
```csharp
namespace ClaudeToolbar.App.Services;

/// <summary>Watches Claude Code's credentials file and calls back (on a thread-pool thread) 500 ms after the last change.</summary>
public sealed class CredentialsWatcher : IDisposable
{
    private readonly FileSystemWatcher? _watcher;
    private readonly Timer _debounce;

    public CredentialsWatcher(string filePath, Action onChanged)
    {
        _debounce = new Timer(_ => onChanged(), null, Timeout.Infinite, Timeout.Infinite);
        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            Log.Info($"Credentials directory missing, not watching: {dir}");
            return;
        }
        _watcher = new FileSystemWatcher(dir, Path.GetFileName(filePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += Bump;
        _watcher.Created += Bump;
        _watcher.Deleted += Bump;
        _watcher.Renamed += Bump;
    }

    private void Bump(object sender, FileSystemEventArgs e) => _debounce.Change(500, Timeout.Infinite);

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce.Dispose();
    }
}
```

- [ ] **Step 2: Widget interaction (hover flyout, click, right-click)**

`src/ClaudeToolbar.App/Widget/WidgetWindow.Interaction.cs`:
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.App.Widget;

public partial class WidgetWindow
{
    private readonly Popup _flyout = new()
    {
        AllowsTransparency = true,
        StaysOpen = true,
        Placement = PlacementMode.Top,
        PopupAnimation = PopupAnimation.Fade,
        VerticalOffset = -8,
    };
    private readonly StackPanel _flyoutPanel = new();
    private readonly Border _flyoutBorder = new()
    {
        Padding = new Thickness(12, 8, 12, 8),
        CornerRadius = new CornerRadius(6),
        BorderThickness = new Thickness(1),
    };
    private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    public event Action? Clicked;
    public event Action? MenuRequested;
    public event Action? FlyoutRequested;

    public bool IsFlyoutOpen => _flyout.IsOpen;

    private void InitializeInteraction()
    {
        _flyoutBorder.Child = _flyoutPanel;
        _flyout.Child = _flyoutBorder;
        _flyout.PlacementTarget = Root;
        _hoverTimer.Tick += (_, _) =>
        {
            _hoverTimer.Stop();
            FlyoutRequested?.Invoke();
        };
        Root.MouseEnter += (_, _) => _hoverTimer.Start();
        Root.MouseLeave += (_, _) =>
        {
            _hoverTimer.Stop();
            HideFlyout();
        };
        Root.MouseLeftButtonUp += (_, _) =>
        {
            HideFlyout();
            Clicked?.Invoke();
        };
        Root.MouseRightButtonUp += (_, _) =>
        {
            HideFlyout();
            MenuRequested?.Invoke();
        };
    }

    public void ShowFlyout(FlyoutModel model, WidgetTheme theme)
    {
        _flyoutBorder.Background = theme.Background;
        _flyoutBorder.BorderBrush = theme.BarTrack;
        _flyoutPanel.Children.Clear();
        foreach (var line in model.Lines)
        {
            _flyoutPanel.Children.Add(new TextBlock
            {
                Text = line,
                Foreground = theme.Text,
                FontFamily = WidgetTheme.Font,
                FontSize = theme.FontSize + 1,
                Margin = new Thickness(0, 1, 0, 1),
            });
        }
        _flyoutPanel.Children.Add(new TextBlock
        {
            Text = model.StatusText,
            Foreground = theme.Text,
            Opacity = 0.7,
            FontFamily = WidgetTheme.Font,
            FontSize = theme.FontSize,
            Margin = new Thickness(0, 4, 0, 0),
        });
        _flyout.IsOpen = true;
    }

    public void HideFlyout() => _flyout.IsOpen = false;
}
```

In `WidgetWindow.xaml.cs` change the constructor to call it:
```csharp
    public WidgetWindow()
    {
        InitializeComponent();
        Root.Children.Add(_rows);
        InitializeInteraction();
    }
```

- [ ] **Step 3: Rewrite `App.Widget.cs` with real data**

Replace the whole file:
```csharp
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Threading;
using ClaudeToolbar.App.Interop;
using ClaudeToolbar.App.Services;
using ClaudeToolbar.App.Widget;
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Time;
using ClaudeToolbar.Core.Usage;
using ClaudeToolbar.Core.Widget;
using Microsoft.Win32;

namespace ClaudeToolbar.App;

public partial class App
{
    private static readonly TimeSpan NoCredentialsRetry = TimeSpan.FromSeconds(30);

    private WidgetWindow? _widget;
    private WidgetController? _controller;
    private UsageMonitor? _monitor;
    private CredentialsWatcher? _credentialsWatcher;
    private HttpClient? _http;
    private DispatcherTimer? _tick;
    private WidgetTheme? _theme;
    private WidgetModel? _model;
    private DateTime _lastNoCredentialsRetry = DateTime.MinValue;
    private bool? _startupApplied;

    public event Action<MonitorState>? MonitorStateChanged;

    public MonitorState? CurrentState => _monitor?.State;

    partial void OnStartupCore(StartupEventArgs e)
    {
        var clock = SystemClock.Instance;
        var credentialsPath = CredentialsPaths.ResolveFromEnvironment();

        _http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) });
        _monitor = new UsageMonitor(
            new FileCredentialsSource(credentialsPath, clock),
            new UsageClient(_http, clock),
            clock,
            new RefreshScheduler(clock, Settings.Behavior.RefreshIntervalSeconds));
        _monitor.StateChanged += state => Dispatcher.InvokeAsync(() => OnMonitorState(state));
        _credentialsWatcher = new CredentialsWatcher(credentialsPath, () => Dispatcher.InvokeAsync(() =>
        {
            Log.Info("Credentials file changed");
            _monitor.OnCredentialsChanged();
        }));

        _widget = new WidgetWindow();
        _widget.Clicked += OpenSettings;
        _widget.MenuRequested += () => _menu?.Show();
        _widget.FlyoutRequested += ShowFlyout;
        _controller = new WidgetController(_widget, new TaskbarTracker(_widget), () => Settings);
        _theme = WidgetTheme.FromSettings(Settings.Appearance);
        RenderWidget(_monitor.State);
        _controller.Start();

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

        _tick = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += (_, _) => Tick();
        _tick.Start();
        Log.Info($"Monitoring credentials at {credentialsPath}");
    }

    partial void RefreshNowCore()
    {
        _monitor?.RequestRefresh();
        _controller?.Relocate();
    }

    /// <summary>Re-applies the current <see cref="Settings"/> to the running widget. Called by the settings window on every change.</summary>
    public void ApplySettingsLive()
    {
        _theme = WidgetTheme.FromSettings(Settings.Appearance);
        if (_monitor is not null) _monitor.Scheduler.IntervalSeconds = Settings.Behavior.RefreshIntervalSeconds;
        if (_startupApplied != Settings.Behavior.RunAtStartup)
        {
            _startupApplied = Settings.Behavior.RunAtStartup;
            TrySafe(() => StartupRegistration.Apply(Settings.Behavior.RunAtStartup), "startup registration");
            _menu?.SetRunAtStartup(Settings.Behavior.RunAtStartup);
        }
        if (_monitor is not null) RenderWidget(_monitor.State);
        _controller?.Reposition();
    }

    private void OnMonitorState(MonitorState state)
    {
        Log.Info($"Usage state: {state.Status}{(state.Message is null ? string.Empty : " — " + state.Message)}");
        RenderWidget(state);
        MonitorStateChanged?.Invoke(state);
    }

    private void RenderWidget(MonitorState state)
    {
        if (_widget is null) return;
        _model = WidgetModelBuilder.Build(state, Settings, DateTimeOffset.UtcNow);
        _widget.Render(_model, Settings.Rows, _theme ??= WidgetTheme.FromSettings(Settings.Appearance));
        _controller?.Reposition();
        Tray?.SetTooltip(BuildTooltip(_model));
    }

    private static string BuildTooltip(WidgetModel model)
    {
        if (model.Rows.Count == 0) return "Claude Toolbar · " + (model.Notice ?? string.Empty);
        return "Claude Toolbar · " + string.Join(" · ", model.Rows.Select(r => $"{r.Label} {r.PercentText}"));
    }

    private void ShowFlyout()
    {
        if (_widget is null || _monitor is null || _theme is null) return;
        var flyout = FlyoutModelBuilder.Build(_monitor.State, DateTimeOffset.UtcNow, t => t.ToLocalTime().ToString("HH:mm"));
        _widget.ShowFlyout(flyout, _theme);
    }

    private void Tick()
    {
        if (_monitor is null || _widget is null) return;

        if (_monitor.State.Status == UsageStatus.NoCredentials && DateTime.UtcNow - _lastNoCredentialsRetry > NoCredentialsRetry)
        {
            _lastNoCredentialsRetry = DateTime.UtcNow;
            _monitor.RequestRefresh();
        }

        _ = SafeTickAsync();

        _model = WidgetModelBuilder.Build(_monitor.State, Settings, DateTimeOffset.UtcNow);
        _widget.UpdateTimes(_model);
        if (_widget.IsFlyoutOpen) ShowFlyout();
    }

    private async Task SafeTickAsync()
    {
        try
        {
            await _monitor!.TickAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log.Error("Usage tick failed", ex);
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume) return;
        Dispatcher.InvokeAsync(() =>
        {
            Log.Info("Resumed from sleep");
            _monitor?.RequestRefresh();
            _controller?.Relocate();
        });
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.InvokeAsync(() => _controller?.Relocate());

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (!e.IsAvailable) return;
        Dispatcher.InvokeAsync(() =>
        {
            Log.Info("Network available");
            _monitor?.RequestRefresh();
        });
    }

    partial void OnExitCore()
    {
        _tick?.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _credentialsWatcher?.Dispose();
        _controller?.Dispose();
        _widget?.Close();
        _http?.Dispose();
    }
}
```

Remove the `RefreshNowCore` line from `App.Placeholders.cs` so the file only contains `OpenSettingsCore`.

- [ ] **Step 4: Build and verify with real data**

```bash
dotnet build src/ClaudeToolbar.App
```
PowerShell:
```powershell
Start-Process "src\ClaudeToolbar.App\bin\Debug\net10.0-windows\win-x64\ClaudeToolbar.exe"; Start-Sleep 5
Get-Content "$env:LOCALAPPDATA\ClaudeToolbar\logs\app.log" -Tail 6
powershell -ExecutionPolicy Bypass -File tools/screenshot-taskbar.ps1 -Out "$env:TEMP\live.png"
```
Expected: log shows `Usage state: Ok` (the user is logged in to Claude Code on this machine) and the screenshot shows real percentages. Wait 65 s and confirm a second fetch happened (the log only logs state changes, so add a temporary `Log.Info` in `OnMonitorState`? No: check `Updated Ns ago` by hovering is not scriptable; instead temporarily set `refreshIntervalSeconds` to 30 in `%APPDATA%\ClaudeToolbar\settings.json`, restart the app, and confirm via a second screenshot that the `Updated` line changes — or simply confirm no errors in the log after 2 minutes.) Then test the expired path: 
```powershell
Stop-Process -Name ClaudeToolbar
New-Item -ItemType Directory -Force "$env:TEMP\fakeclaude" | Out-Null
'{ "claudeAiOauth": { "accessToken": "x", "refreshToken": "y", "expiresAt": 1, "subscriptionType": "max" } }' | Set-Content "$env:TEMP\fakeclaude\.credentials.json" -Encoding utf8
$env:CLAUDE_CONFIG_DIR = "$env:TEMP\fakeclaude"
Start-Process "src\ClaudeToolbar.App\bin\Debug\net10.0-windows\win-x64\ClaudeToolbar.exe"; Start-Sleep 4
powershell -ExecutionPolicy Bypass -File tools/screenshot-taskbar.ps1 -Out "$env:TEMP\expired.png"
Remove-Item "$env:TEMP\fakeclaude\.credentials.json"; Start-Sleep 2
powershell -ExecutionPolicy Bypass -File tools/screenshot-taskbar.ps1 -Out "$env:TEMP\missing.png"
Stop-Process -Name ClaudeToolbar; Remove-Item Env:CLAUDE_CONFIG_DIR
```
Expected: `expired.png` shows dimmed rows with `↻ run claude`; `missing.png` shows `Sign in with claude` (the watcher picked up the deletion). Log lines: `Usage state: Expired`, `Credentials file changed`, `Usage state: NoCredentials`.

- [ ] **Step 5: Commit and push**

```bash
git add -A
git commit -m "Wire live usage data, credentials watching, system events and widget flyout"
git push origin main
```

---

### Task 15: Settings window

**Files:**
- Create: `src/ClaudeToolbar.App/Services/SystemTheme.cs`
- Create: `src/ClaudeToolbar.App/Settings/Styles.xaml`, `EqualsConverter.cs`, `ColorEditor.xaml`, `ColorEditor.xaml.cs`, `SettingsViewModel.cs`, `SettingsWindow.xaml`, `SettingsWindow.xaml.cs`
- Create: `src/ClaudeToolbar.App/App.Settings.cs`
- Modify: `src/ClaudeToolbar.App/App.xaml` (merge Styles.xaml)
- Delete: `src/ClaudeToolbar.App/App.Placeholders.cs`

**Interfaces:**
- Consumes: `AppSettings`, `Presets`, `SettingsValidator` (Task 6), `UsageRowsControl`, `WidgetTheme` (Task 12), `WidgetModelBuilder` (Task 9), `App.ApplySettingsLive()`, `App.SaveSettingsDebounced()`, `App.MonitorStateChanged`, `App.CurrentState`, `App.RefreshNow()` (Tasks 10, 14), `MonitorState`, `CredentialsState`.
- Produces: `SystemTheme.IsLight()`; `SettingsViewModel(AppSettings settings, Action onChanged)` with one property per setting plus `ReloadFrom(AppSettings)` and `UpdateAccount(MonitorState?)`; `SettingsWindow(SettingsViewModel)`; `App.OpenSettings()` shows/focuses it.

- [ ] **Step 1: Theme detection and resources**

`src/ClaudeToolbar.App/Services/SystemTheme.cs`:
```csharp
using Microsoft.Win32;

namespace ClaudeToolbar.App.Services;

public static class SystemTheme
{
    public static bool IsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException)
        {
            return false;
        }
    }
}
```

`src/ClaudeToolbar.App/Settings/Styles.xaml`:
```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Overridden at runtime by App.ApplyTheme(); these are the dark defaults. -->
    <SolidColorBrush x:Key="WindowBg" Color="#FF202020" />
    <SolidColorBrush x:Key="CardBg" Color="#FF2B2B2B" />
    <SolidColorBrush x:Key="InputBg" Color="#FF1E1E1E" />
    <SolidColorBrush x:Key="TextPrimary" Color="#FFF3F3F3" />
    <SolidColorBrush x:Key="TextSecondary" Color="#FFA6A6A6" />
    <SolidColorBrush x:Key="Accent" Color="#FFD97757" />
    <SolidColorBrush x:Key="BorderBrushKey" Color="#FF3A3A3A" />

    <FontFamily x:Key="UiFont">Segoe UI Variable Text, Segoe UI</FontFamily>

    <Style x:Key="SectionHeader" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource UiFont}" />
        <Setter Property="FontSize" Value="15" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Foreground" Value="{DynamicResource TextPrimary}" />
        <Setter Property="Margin" Value="0,0,0,8" />
    </Style>

    <Style x:Key="FieldLabel" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource UiFont}" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="Foreground" Value="{DynamicResource TextSecondary}" />
        <Setter Property="VerticalAlignment" Value="Center" />
        <Setter Property="Margin" Value="0,0,12,0" />
    </Style>

    <Style x:Key="Body" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource UiFont}" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="Foreground" Value="{DynamicResource TextPrimary}" />
        <Setter Property="VerticalAlignment" Value="Center" />
        <Setter Property="TextWrapping" Value="Wrap" />
    </Style>

    <Style x:Key="Card" TargetType="Border">
        <Setter Property="Background" Value="{DynamicResource CardBg}" />
        <Setter Property="BorderBrush" Value="{DynamicResource BorderBrushKey}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="Padding" Value="16,12" />
        <Setter Property="Margin" Value="0,0,0,12" />
    </Style>

    <Style TargetType="Button">
        <Setter Property="FontFamily" Value="{StaticResource UiFont}" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="Foreground" Value="{DynamicResource TextPrimary}" />
        <Setter Property="Background" Value="{DynamicResource InputBg}" />
        <Setter Property="BorderBrush" Value="{DynamicResource BorderBrushKey}" />
        <Setter Property="Padding" Value="12,6" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="6" Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource Accent}" />
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="Bd" Property="Opacity" Value="0.8" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key="Chip" TargetType="RadioButton">
        <Setter Property="FontFamily" Value="{StaticResource UiFont}" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="Foreground" Value="{DynamicResource TextPrimary}" />
        <Setter Property="Margin" Value="0,0,8,0" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="RadioButton">
                    <Border x:Name="Bd" Background="{DynamicResource InputBg}" BorderBrush="{DynamicResource BorderBrushKey}" BorderThickness="1" CornerRadius="14" Padding="12,5">
                        <ContentPresenter VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{DynamicResource Accent}" />
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource Accent}" />
                            <Setter Property="Foreground" Value="White" />
                        </Trigger>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource Accent}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType="TextBox">
        <Setter Property="FontFamily" Value="{StaticResource UiFont}" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="Foreground" Value="{DynamicResource TextPrimary}" />
        <Setter Property="Background" Value="{DynamicResource InputBg}" />
        <Setter Property="BorderBrush" Value="{DynamicResource BorderBrushKey}" />
        <Setter Property="CaretBrush" Value="{DynamicResource TextPrimary}" />
        <Setter Property="Padding" Value="6,4" />
    </Style>

    <Style TargetType="CheckBox">
        <Setter Property="FontFamily" Value="{StaticResource UiFont}" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="Foreground" Value="{DynamicResource TextPrimary}" />
        <Setter Property="Margin" Value="0,4,16,4" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
    </Style>

    <Style TargetType="Slider">
        <Setter Property="Margin" Value="0,2" />
        <Setter Property="IsSnapToTickEnabled" Value="True" />
        <Setter Property="TickFrequency" Value="1" />
        <Setter Property="VerticalAlignment" Value="Center" />
    </Style>
</ResourceDictionary>
```

Change `App.xaml` resources to merge it:
```xml
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Settings/Styles.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
```

`src/ClaudeToolbar.App/Settings/EqualsConverter.cs`:
```csharp
using System.Globalization;
using System.Windows.Data;

namespace ClaudeToolbar.App.Settings;

/// <summary>Binds a string property to a RadioButton: checked when the value equals the parameter.</summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? parameter?.ToString() ?? string.Empty : Binding.DoNothing;
}
```

- [ ] **Step 2: Color editor**

`src/ClaudeToolbar.App/Settings/ColorEditor.xaml`:
```xml
<UserControl x:Class="ClaudeToolbar.App.Settings.ColorEditor"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <StackPanel Orientation="Horizontal">
        <Button x:Name="Swatch" Width="30" Height="24" Padding="2" Click="Swatch_Click">
            <Border x:Name="SwatchFill" CornerRadius="3" />
        </Button>
        <TextBox x:Name="Hex" Width="100" Margin="6,0,0,0" VerticalContentAlignment="Center" LostFocus="Hex_LostFocus" KeyDown="Hex_KeyDown" />
        <Popup x:Name="Picker" PlacementTarget="{Binding ElementName=Swatch}" Placement="Bottom" StaysOpen="False" AllowsTransparency="True">
            <Border Background="{DynamicResource CardBg}" BorderBrush="{DynamicResource BorderBrushKey}" BorderThickness="1" CornerRadius="6" Padding="12" Width="240">
                <StackPanel>
                    <TextBlock Text="Hue" Style="{StaticResource FieldLabel}" />
                    <Slider x:Name="H" Minimum="0" Maximum="360" ValueChanged="Slider_Changed" />
                    <TextBlock Text="Saturation" Style="{StaticResource FieldLabel}" />
                    <Slider x:Name="S" Minimum="0" Maximum="100" ValueChanged="Slider_Changed" />
                    <TextBlock Text="Brightness" Style="{StaticResource FieldLabel}" />
                    <Slider x:Name="V" Minimum="0" Maximum="100" ValueChanged="Slider_Changed" />
                    <TextBlock Text="Opacity" Style="{StaticResource FieldLabel}" />
                    <Slider x:Name="A" Minimum="0" Maximum="100" ValueChanged="Slider_Changed" />
                </StackPanel>
            </Border>
        </Popup>
    </StackPanel>
</UserControl>
```

`src/ClaudeToolbar.App/Settings/ColorEditor.xaml.cs`:
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.App.Settings;

public partial class ColorEditor : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(ColorEditor),
        new FrameworkPropertyMetadata("#FF000000", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    private bool _updating;

    public ColorEditor()
    {
        InitializeComponent();
        SyncFromValue();
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ColorEditor)d).SyncFromValue();

    private void SyncFromValue()
    {
        if (_updating) return;
        _updating = true;
        try
        {
            var text = SettingsValidator.NormalizeColor(Value, "#FF000000");
            var color = (Color)ColorConverter.ConvertFromString(text);
            Hex.Text = text;
            SwatchFill.Background = new SolidColorBrush(color);
            var (h, s, v) = ToHsv(color);
            H.Value = h;
            S.Value = s * 100;
            V.Value = v * 100;
            A.Value = color.A / 255.0 * 100;
        }
        finally
        {
            _updating = false;
        }
    }

    private void Swatch_Click(object sender, RoutedEventArgs e) => Picker.IsOpen = !Picker.IsOpen;

    private void Hex_LostFocus(object sender, RoutedEventArgs e) => CommitHex();

    private void Hex_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitHex();
    }

    private void CommitHex()
    {
        var text = Hex.Text.Trim();
        if (!text.StartsWith('#')) text = "#" + text;
        if (SettingsValidator.IsValidColor(text) || SettingsValidator.NormalizeColor(text, "") != "")
            Value = SettingsValidator.NormalizeColor(text, Value);
        else
            SyncFromValue();
    }

    private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating) return;
        var color = FromHsv(H.Value, S.Value / 100, V.Value / 100);
        color.A = (byte)Math.Round(A.Value / 100 * 255);
        Value = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static (double H, double S, double V) ToHsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        double h = 0;
        if (delta > 0)
        {
            if (max == r) h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * ((b - r) / delta + 2);
            else h = 60 * ((r - g) / delta + 4);
            if (h < 0) h += 360;
        }
        var s = max == 0 ? 0 : delta / max;
        return (h, s, max);
    }

    private static Color FromHsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        double r, g, b;
        if (h < 60) (r, g, b) = (c, x, 0);
        else if (h < 120) (r, g, b) = (x, c, 0);
        else if (h < 180) (r, g, b) = (0, c, x);
        else if (h < 240) (r, g, b) = (0, x, c);
        else if (h < 300) (r, g, b) = (x, 0, c);
        else (r, g, b) = (c, 0, x);
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
```

- [ ] **Step 3: View model**

`src/ClaudeToolbar.App/Settings/SettingsViewModel.cs`:
```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Settings;

namespace ClaudeToolbar.App.Settings;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly Action _onChanged;
    private AppSettings _s;

    public SettingsViewModel(AppSettings settings, Action onChanged)
    {
        _s = settings;
        _onChanged = onChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppSettings Settings => _s;

    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void Changed([CallerMemberName] string? name = null)
    {
        SettingsValidator.Normalize(_s);
        Raise(name);
        _onChanged();
    }

    private void SetColor(Action<string> assign, string value, [CallerMemberName] string? name = null)
    {
        assign(SettingsValidator.NormalizeColor(value, "#FF000000"));
        if (_s.Appearance.Preset != Presets.Custom)
        {
            _s.Appearance.Preset = Presets.Custom;
            Raise(nameof(Preset));
        }
        Changed(name);
    }

    // Appearance
    public string Preset
    {
        get => _s.Appearance.Preset;
        set
        {
            if (!Presets.TryApply(value, _s.Appearance)) return;
            Changed();
            RaiseAllColors();
        }
    }

    public string Background { get => _s.Appearance.Background; set => SetColor(v => _s.Appearance.Background = v, value); }
    public string Text { get => _s.Appearance.Text; set => SetColor(v => _s.Appearance.Text = v, value); }
    public string BarTrack { get => _s.Appearance.BarTrack; set => SetColor(v => _s.Appearance.BarTrack = v, value); }
    public string BarOk { get => _s.Appearance.BarOk; set => SetColor(v => _s.Appearance.BarOk = v, value); }
    public string BarWarn { get => _s.Appearance.BarWarn; set => SetColor(v => _s.Appearance.BarWarn = v, value); }
    public string BarCrit { get => _s.Appearance.BarCrit; set => SetColor(v => _s.Appearance.BarCrit = v, value); }

    public double FontSize { get => _s.Appearance.FontSize; set { _s.Appearance.FontSize = value; Changed(); } }
    public double CornerRadius { get => _s.Appearance.CornerRadius; set { _s.Appearance.CornerRadius = value; Changed(); } }

    public double WarnThreshold
    {
        get => _s.Appearance.WarnThreshold;
        set
        {
            var warn = (int)Math.Round(value);
            _s.Appearance.WarnThreshold = warn;
            if (warn >= _s.Appearance.CritThreshold)
            {
                _s.Appearance.CritThreshold = Math.Min(100, warn + 1);
                Raise(nameof(CritThreshold));
            }
            Changed();
        }
    }

    public double CritThreshold
    {
        get => _s.Appearance.CritThreshold;
        set
        {
            var crit = (int)Math.Round(value);
            _s.Appearance.CritThreshold = crit;
            if (crit <= _s.Appearance.WarnThreshold)
            {
                _s.Appearance.WarnThreshold = Math.Max(1, crit - 1);
                Raise(nameof(WarnThreshold));
            }
            Changed();
        }
    }

    // Rows
    public bool ShowFiveHour { get => _s.Rows.ShowFiveHour; set { _s.Rows.ShowFiveHour = value; Changed(); } }
    public bool ShowSevenDay { get => _s.Rows.ShowSevenDay; set { _s.Rows.ShowSevenDay = value; Changed(); } }
    public bool ShowSevenDayOpus { get => _s.Rows.ShowSevenDayOpus; set { _s.Rows.ShowSevenDayOpus = value; Changed(); } }
    public bool ShowSevenDaySonnet { get => _s.Rows.ShowSevenDaySonnet; set { _s.Rows.ShowSevenDaySonnet = value; Changed(); } }
    public bool ShowLabel { get => _s.Rows.ShowLabel; set { _s.Rows.ShowLabel = value; Changed(); } }
    public bool ShowBar { get => _s.Rows.ShowBar; set { _s.Rows.ShowBar = value; Changed(); } }
    public bool ShowPercent { get => _s.Rows.ShowPercent; set { _s.Rows.ShowPercent = value; Changed(); } }
    public bool ShowTime { get => _s.Rows.ShowTime; set { _s.Rows.ShowTime = value; Changed(); } }
    public double BarWidth { get => _s.Rows.BarWidth; set { _s.Rows.BarWidth = value; Changed(); } }

    // Behaviour
    public double RefreshIntervalSeconds { get => _s.Behavior.RefreshIntervalSeconds; set { _s.Behavior.RefreshIntervalSeconds = (int)Math.Round(value); Changed(); } }
    public double TrayGapPx { get => _s.Behavior.TrayGapPx; set { _s.Behavior.TrayGapPx = (int)Math.Round(value); Changed(); } }
    public bool HideInFullscreen { get => _s.Behavior.HideInFullscreen; set { _s.Behavior.HideInFullscreen = value; Changed(); } }
    public bool RunAtStartup { get => _s.Behavior.RunAtStartup; set { _s.Behavior.RunAtStartup = value; Changed(); } }

    // Account (read-only, fed by UpdateAccount)
    public string CredentialsPath { get; private set; } = string.Empty;
    public string TokenStateText { get; private set; } = "Unknown";
    public string SubscriptionText { get; private set; } = "—";
    public string LastUpdateText { get; private set; } = "Never";
    public string HintText { get; private set; } = string.Empty;
    public bool HintVisible { get; private set; }

    public void UpdateAccount(MonitorState? state)
    {
        if (state is null) return;
        CredentialsPath = state.Credentials switch
        {
            CredentialsState.Missing m => m.Path,
            CredentialsState.Invalid i => i.Path,
            CredentialsState.Expired e => e.Path,
            CredentialsState.Valid v => v.Path,
            _ => string.Empty,
        };
        TokenStateText = state.Credentials switch
        {
            CredentialsState.Valid v => $"Valid until {v.ExpiresAt.ToLocalTime():HH:mm}",
            CredentialsState.Expired e => $"Expired at {e.ExpiresAt.ToLocalTime():HH:mm}",
            CredentialsState.Invalid i => $"Unreadable: {i.Reason}",
            _ => "Not found",
        };
        SubscriptionText = state.Credentials switch
        {
            CredentialsState.Valid v => v.SubscriptionType ?? "—",
            CredentialsState.Expired e => e.SubscriptionType ?? "—",
            _ => "—",
        };
        LastUpdateText = state.LastSuccess is { } last ? $"{last.ToLocalTime():HH:mm:ss}" : "Never";
        HintVisible = state.Status is UsageStatus.Expired or UsageStatus.NoCredentials;
        HintText = HintVisible ? "Run `claude` in a terminal to refresh your login." : string.Empty;
        Raise(nameof(CredentialsPath));
        Raise(nameof(TokenStateText));
        Raise(nameof(SubscriptionText));
        Raise(nameof(LastUpdateText));
        Raise(nameof(HintText));
        Raise(nameof(HintVisible));
    }

    /// <summary>Copies values from another settings object into the live one and refreshes every binding.</summary>
    public void ReloadFrom(AppSettings source)
    {
        var json = SettingsJson.Serialize(source);
        var fresh = SettingsValidator.Normalize(SettingsJson.Deserialize(json));
        _s.Appearance = fresh.Appearance;
        _s.Rows = fresh.Rows;
        _s.Behavior = fresh.Behavior;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        _onChanged();
    }

    private void RaiseAllColors()
    {
        foreach (var n in new[] { nameof(Background), nameof(Text), nameof(BarTrack), nameof(BarOk), nameof(BarWarn), nameof(BarCrit) })
            Raise(n);
    }
}
```

- [ ] **Step 4: Settings window**

`src/ClaudeToolbar.App/Settings/SettingsWindow.xaml`:
```xml
<Window x:Class="ClaudeToolbar.App.Settings.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:ClaudeToolbar.App.Settings"
        Title="Claude Toolbar Settings"
        Width="580" Height="760" MinWidth="520" MinHeight="520"
        WindowStartupLocation="CenterScreen"
        Background="{DynamicResource WindowBg}"
        Icon="pack://application:,,,/Assets/app.ico"
        TextElement.Foreground="{DynamicResource TextPrimary}"
        FontFamily="{StaticResource UiFont}">
    <Window.Resources>
        <local:EqualsConverter x:Key="Equals" />
        <Style x:Key="Row" TargetType="Grid">
            <Setter Property="Margin" Value="0,4" />
        </Style>
    </Window.Resources>
    <DockPanel Margin="16">
        <!-- Live preview -->
        <Border DockPanel.Dock="Top" Background="#FF1B1B1B" CornerRadius="8" Height="60" Margin="0,0,0,12">
            <Grid>
                <TextBlock Text="Preview" Style="{StaticResource FieldLabel}" Foreground="#FF8A8A8A" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="10,6,0,0" />
                <ContentControl x:Name="PreviewHost" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,16,0" />
            </Grid>
        </Border>

        <!-- Footer -->
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
            <Button Content="Reset to defaults" Click="Reset_Click" Margin="0,0,8,0" />
            <Button Content="Close" Click="Close_Click" />
        </StackPanel>

        <ScrollViewer VerticalScrollBarVisibility="Auto">
            <StackPanel>
                <!-- Appearance -->
                <Border Style="{StaticResource Card}">
                    <StackPanel>
                        <TextBlock Text="Appearance" Style="{StaticResource SectionHeader}" />
                        <StackPanel Orientation="Horizontal" Margin="0,0,0,10">
                            <RadioButton Content="Dark" Style="{StaticResource Chip}" IsChecked="{Binding Preset, Converter={StaticResource Equals}, ConverterParameter=dark}" />
                            <RadioButton Content="Light" Style="{StaticResource Chip}" IsChecked="{Binding Preset, Converter={StaticResource Equals}, ConverterParameter=light}" />
                            <RadioButton Content="Claude" Style="{StaticResource Chip}" IsChecked="{Binding Preset, Converter={StaticResource Equals}, ConverterParameter=claude}" />
                            <RadioButton Content="Mono" Style="{StaticResource Chip}" IsChecked="{Binding Preset, Converter={StaticResource Equals}, ConverterParameter=mono}" />
                        </StackPanel>
                        <Grid Style="{StaticResource Row}">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="130" /><ColumnDefinition Width="*" />
                                <ColumnDefinition Width="110" /><ColumnDefinition Width="*" />
                            </Grid.ColumnDefinitions>
                            <Grid.RowDefinitions>
                                <RowDefinition /><RowDefinition /><RowDefinition />
                            </Grid.RowDefinitions>
                            <TextBlock Grid.Row="0" Grid.Column="0" Text="Background" Style="{StaticResource FieldLabel}" />
                            <local:ColorEditor Grid.Row="0" Grid.Column="1" Value="{Binding Background}" Margin="0,3" />
                            <TextBlock Grid.Row="0" Grid.Column="2" Text="Text" Style="{StaticResource FieldLabel}" />
                            <local:ColorEditor Grid.Row="0" Grid.Column="3" Value="{Binding Text}" Margin="0,3" />
                            <TextBlock Grid.Row="1" Grid.Column="0" Text="Bar track" Style="{StaticResource FieldLabel}" />
                            <local:ColorEditor Grid.Row="1" Grid.Column="1" Value="{Binding BarTrack}" Margin="0,3" />
                            <TextBlock Grid.Row="1" Grid.Column="2" Text="Bar OK" Style="{StaticResource FieldLabel}" />
                            <local:ColorEditor Grid.Row="1" Grid.Column="3" Value="{Binding BarOk}" Margin="0,3" />
                            <TextBlock Grid.Row="2" Grid.Column="0" Text="Bar warning" Style="{StaticResource FieldLabel}" />
                            <local:ColorEditor Grid.Row="2" Grid.Column="1" Value="{Binding BarWarn}" Margin="0,3" />
                            <TextBlock Grid.Row="2" Grid.Column="2" Text="Bar critical" Style="{StaticResource FieldLabel}" />
                            <local:ColorEditor Grid.Row="2" Grid.Column="3" Value="{Binding BarCrit}" Margin="0,3" />
                        </Grid>
                        <Grid Style="{StaticResource Row}">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="130" /><ColumnDefinition Width="*" /><ColumnDefinition Width="40" />
                            </Grid.ColumnDefinitions>
                            <Grid.RowDefinitions>
                                <RowDefinition /><RowDefinition /><RowDefinition /><RowDefinition />
                            </Grid.RowDefinitions>
                            <TextBlock Grid.Row="0" Text="Font size" Style="{StaticResource FieldLabel}" />
                            <Slider Grid.Row="0" Grid.Column="1" Minimum="9" Maximum="14" Value="{Binding FontSize}" />
                            <TextBlock Grid.Row="0" Grid.Column="2" Text="{Binding FontSize, StringFormat={}{0:0}}" Style="{StaticResource Body}" TextAlignment="Right" />
                            <TextBlock Grid.Row="1" Text="Corner radius" Style="{StaticResource FieldLabel}" />
                            <Slider Grid.Row="1" Grid.Column="1" Minimum="0" Maximum="12" Value="{Binding CornerRadius}" />
                            <TextBlock Grid.Row="1" Grid.Column="2" Text="{Binding CornerRadius, StringFormat={}{0:0}}" Style="{StaticResource Body}" TextAlignment="Right" />
                            <TextBlock Grid.Row="2" Text="Warning at" Style="{StaticResource FieldLabel}" />
                            <Slider Grid.Row="2" Grid.Column="1" Minimum="1" Maximum="99" Value="{Binding WarnThreshold}" />
                            <TextBlock Grid.Row="2" Grid.Column="2" Text="{Binding WarnThreshold, StringFormat={}{0:0}%}" Style="{StaticResource Body}" TextAlignment="Right" />
                            <TextBlock Grid.Row="3" Text="Critical at" Style="{StaticResource FieldLabel}" />
                            <Slider Grid.Row="3" Grid.Column="1" Minimum="2" Maximum="100" Value="{Binding CritThreshold}" />
                            <TextBlock Grid.Row="3" Grid.Column="2" Text="{Binding CritThreshold, StringFormat={}{0:0}%}" Style="{StaticResource Body}" TextAlignment="Right" />
                        </Grid>
                    </StackPanel>
                </Border>

                <!-- Rows -->
                <Border Style="{StaticResource Card}">
                    <StackPanel>
                        <TextBlock Text="Rows" Style="{StaticResource SectionHeader}" />
                        <WrapPanel>
                            <CheckBox Content="Session (5h)" IsChecked="{Binding ShowFiveHour}" />
                            <CheckBox Content="Weekly (7d)" IsChecked="{Binding ShowSevenDay}" />
                            <CheckBox Content="Weekly Opus" IsChecked="{Binding ShowSevenDayOpus}" />
                            <CheckBox Content="Weekly Sonnet" IsChecked="{Binding ShowSevenDaySonnet}" />
                        </WrapPanel>
                        <WrapPanel Margin="0,6,0,0">
                            <CheckBox Content="Label" IsChecked="{Binding ShowLabel}" />
                            <CheckBox Content="Bar" IsChecked="{Binding ShowBar}" />
                            <CheckBox Content="Percent" IsChecked="{Binding ShowPercent}" />
                            <CheckBox Content="Time left" IsChecked="{Binding ShowTime}" />
                        </WrapPanel>
                        <Grid Style="{StaticResource Row}">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="130" /><ColumnDefinition Width="*" /><ColumnDefinition Width="40" />
                            </Grid.ColumnDefinitions>
                            <TextBlock Text="Bar width" Style="{StaticResource FieldLabel}" />
                            <Slider Grid.Column="1" Minimum="30" Maximum="120" Value="{Binding BarWidth}" />
                            <TextBlock Grid.Column="2" Text="{Binding BarWidth, StringFormat={}{0:0}}" Style="{StaticResource Body}" TextAlignment="Right" />
                        </Grid>
                    </StackPanel>
                </Border>

                <!-- Behaviour -->
                <Border Style="{StaticResource Card}">
                    <StackPanel>
                        <TextBlock Text="Behaviour" Style="{StaticResource SectionHeader}" />
                        <Grid Style="{StaticResource Row}">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="130" /><ColumnDefinition Width="*" /><ColumnDefinition Width="40" />
                            </Grid.ColumnDefinitions>
                            <Grid.RowDefinitions>
                                <RowDefinition /><RowDefinition />
                            </Grid.RowDefinitions>
                            <TextBlock Grid.Row="0" Text="Refresh every" Style="{StaticResource FieldLabel}" />
                            <Slider Grid.Row="0" Grid.Column="1" Minimum="30" Maximum="300" TickFrequency="10" Value="{Binding RefreshIntervalSeconds}" />
                            <TextBlock Grid.Row="0" Grid.Column="2" Text="{Binding RefreshIntervalSeconds, StringFormat={}{0:0}s}" Style="{StaticResource Body}" TextAlignment="Right" />
                            <TextBlock Grid.Row="1" Text="Gap from tray" Style="{StaticResource FieldLabel}" />
                            <Slider Grid.Row="1" Grid.Column="1" Minimum="0" Maximum="24" Value="{Binding TrayGapPx}" />
                            <TextBlock Grid.Row="1" Grid.Column="2" Text="{Binding TrayGapPx, StringFormat={}{0:0}px}" Style="{StaticResource Body}" TextAlignment="Right" />
                        </Grid>
                        <WrapPanel Margin="0,6,0,0">
                            <CheckBox Content="Hide when a fullscreen app is active" IsChecked="{Binding HideInFullscreen}" />
                            <CheckBox Content="Run at startup" IsChecked="{Binding RunAtStartup}" />
                        </WrapPanel>
                    </StackPanel>
                </Border>

                <!-- Account -->
                <Border Style="{StaticResource Card}">
                    <StackPanel>
                        <TextBlock Text="Account" Style="{StaticResource SectionHeader}" />
                        <Grid Style="{StaticResource Row}">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="130" /><ColumnDefinition Width="*" />
                            </Grid.ColumnDefinitions>
                            <Grid.RowDefinitions>
                                <RowDefinition /><RowDefinition /><RowDefinition /><RowDefinition />
                            </Grid.RowDefinitions>
                            <TextBlock Grid.Row="0" Text="Credentials file" Style="{StaticResource FieldLabel}" />
                            <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding CredentialsPath}" Style="{StaticResource Body}" />
                            <TextBlock Grid.Row="1" Text="Login" Style="{StaticResource FieldLabel}" />
                            <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding TokenStateText}" Style="{StaticResource Body}" />
                            <TextBlock Grid.Row="2" Text="Plan" Style="{StaticResource FieldLabel}" />
                            <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding SubscriptionText}" Style="{StaticResource Body}" />
                            <TextBlock Grid.Row="3" Text="Last update" Style="{StaticResource FieldLabel}" />
                            <TextBlock Grid.Row="3" Grid.Column="1" Text="{Binding LastUpdateText}" Style="{StaticResource Body}" />
                        </Grid>
                        <TextBlock Text="{Binding HintText}" Style="{StaticResource Body}" Foreground="{DynamicResource Accent}" Margin="0,4,0,8"
                                   Visibility="{Binding HintVisible, Converter={x:Static local:SettingsWindow.BoolToVisibility}}" />
                        <Button Content="Refresh now" HorizontalAlignment="Left" Click="Refresh_Click" />
                        <TextBlock Style="{StaticResource FieldLabel}" Margin="0,10,0,0" TextWrapping="Wrap"
                                   Text="This app only reads the login that Claude Code stores on this machine and calls the read-only usage endpoint. It never writes or refreshes the token." />
                    </StackPanel>
                </Border>
            </StackPanel>
        </ScrollViewer>
    </DockPanel>
</Window>
```

`src/ClaudeToolbar.App/Settings/SettingsWindow.xaml.cs`:
```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ClaudeToolbar.App.Widget;
using ClaudeToolbar.Core.Credentials;
using ClaudeToolbar.Core.Refresh;
using ClaudeToolbar.Core.Settings;
using ClaudeToolbar.Core.Usage;
using ClaudeToolbar.Core.Widget;

namespace ClaudeToolbar.App.Settings;

public partial class SettingsWindow : Window
{
    public static readonly BooleanToVisibilityConverter BoolToVisibility = new();

    private readonly SettingsViewModel _vm;
    private readonly UsageRowsControl _preview = new();

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        PreviewHost.Content = _preview;
        _vm.PropertyChanged += OnVmChanged;
        RenderPreview();
    }

    public SettingsViewModel ViewModel => _vm;

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e) => RenderPreview();

    private void RenderPreview()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new UsageSnapshot(
            new UsageWindow(42, now.AddHours(2).AddMinutes(13)),
            new UsageWindow(75, now.AddDays(3).AddHours(4)),
            new UsageWindow(93, now.AddDays(3)),
            new UsageWindow(12, now.AddDays(2)),
            now);
        var state = new MonitorState(UsageStatus.Ok, snapshot, now, null, new CredentialsState.Missing("preview"));
        var model = WidgetModelBuilder.Build(state, _vm.Settings, now);
        _preview.Render(model, _vm.Settings.Rows, WidgetTheme.FromSettings(_vm.Settings.Appearance));
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => _vm.ReloadFrom(AppSettings.CreateDefault());

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Refresh_Click(object sender, RoutedEventArgs e) => App.Current.RefreshNow();

    protected override void OnClosed(EventArgs e)
    {
        _vm.PropertyChanged -= OnVmChanged;
        base.OnClosed(e);
    }
}
```

- [ ] **Step 5: App wiring and theme**

`src/ClaudeToolbar.App/App.Settings.cs`:
```csharp
using System.Windows;
using System.Windows.Media;
using ClaudeToolbar.App.Services;
using ClaudeToolbar.App.Settings;
using ClaudeToolbar.Core.Refresh;

namespace ClaudeToolbar.App;

public partial class App
{
    private SettingsWindow? _settingsWindow;
    private SettingsViewModel? _settingsViewModel;

    partial void OpenSettingsCore()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }

        ApplyTheme();
        _settingsViewModel = new SettingsViewModel(Settings, () =>
        {
            ApplySettingsLive();
            SaveSettingsDebounced();
        });
        _settingsViewModel.UpdateAccount(CurrentState);
        MonitorStateChanged += OnStateForSettings;

        _settingsWindow = new SettingsWindow(_settingsViewModel);
        _settingsWindow.Closed += (_, _) =>
        {
            MonitorStateChanged -= OnStateForSettings;
            _settingsWindow = null;
            _settingsViewModel = null;
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnStateForSettings(MonitorState state) => _settingsViewModel?.UpdateAccount(state);

    private void ApplyTheme()
    {
        var light = SystemTheme.IsLight();
        Resources["WindowBg"] = Brush(light ? "#FFF3F3F3" : "#FF202020");
        Resources["CardBg"] = Brush(light ? "#FFFFFFFF" : "#FF2B2B2B");
        Resources["InputBg"] = Brush(light ? "#FFFFFFFF" : "#FF1E1E1E");
        Resources["TextPrimary"] = Brush(light ? "#FF1B1B1B" : "#FFF3F3F3");
        Resources["TextSecondary"] = Brush(light ? "#FF5F5F5F" : "#FFA6A6A6");
        Resources["Accent"] = Brush("#FFD97757");
        Resources["BorderBrushKey"] = Brush(light ? "#FFE0E0E0" : "#FF3A3A3A");
    }

    private static SolidColorBrush Brush(string argb)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(argb));
        b.Freeze();
        return b;
    }
}
```

Delete `src/ClaudeToolbar.App/App.Placeholders.cs`.

- [ ] **Step 6: Build and verify**

```bash
dotnet build src/ClaudeToolbar.App
```
PowerShell:
```powershell
Start-Process "src\ClaudeToolbar.App\bin\Debug\net10.0-windows\win-x64\ClaudeToolbar.exe"; Start-Sleep 4
Start-Process "src\ClaudeToolbar.App\bin\Debug\net10.0-windows\win-x64\ClaudeToolbar.exe"; Start-Sleep 3   # second launch opens settings
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type 'using System.Runtime.InteropServices; public static class D { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }'
[D]::SetProcessDPIAware() | Out-Null
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
$bmp.Save("$env:TEMP\settings.png", [System.Drawing.Imaging.ImageFormat]::Png)
```
Read `%TEMP%\settings.png`. Expected: the settings window centred on screen with the preview strip on top showing the sample rows, four cards, chips, colour editors, sliders, and the Account card showing the real credentials path and "Valid until HH:mm". Then edit `%APPDATA%\ClaudeToolbar\settings.json` is NOT the test here; instead, to verify live apply without a mouse, run:
```powershell
# Simulate a preset change through the UI is manual; verify persistence instead:
Stop-Process -Name ClaudeToolbar; Start-Sleep 1
Get-Content "$env:APPDATA\ClaudeToolbar\settings.json"
```
Expected: the file is valid JSON with `version: 1` and the same values the UI showed. If a human is available: click "Claude" preset → widget turns orange immediately; drag "Font size" → widget text grows and the widget keeps its right edge anchored; click "Reset to defaults" → back to dark.

- [ ] **Step 7: Commit and push**

```bash
git add -A
git commit -m "Add settings window with live preview, presets and colour editors"
git push origin main
```

---

### Task 16: README, verification checklist, release build, CI green

**Files:**
- Create: `README.md`, `docs/verification.md`
- Verify: `.github/workflows/build.yml` run is green

- [ ] **Step 1: Write the verification checklist**

`docs/verification.md`:
```markdown
# Manual verification checklist

Run with a Release build: `dotnet publish src/ClaudeToolbar.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish` then start `publish\ClaudeToolbar.exe`.

| # | Check | Expected | Result |
|---|-------|----------|--------|
| 1 | Launch | Widget appears left of the tray chevron within 2 s, vertically centred, two rows | |
| 2 | Real data | Rows show real percentages and countdowns; hover flyout shows "Updated Ns ago" and reset clock times | |
| 3 | Countdown | The minutes value ticks without any click (watch for 2 minutes) | |
| 4 | Drag a window over it | Widget stays on top | |
| 5 | Click desktop / open Start | Widget stays visible; Start menu opens normally | |
| 6 | Focus | With Notepad active, hover and left-click the widget: settings opens, Notepad was not disturbed before the click | |
| 7 | Right-click | Menu with Refresh now / Settings… / Run at startup / Exit; closes when clicking elsewhere | |
| 8 | Tray icon | Right-click shows the same menu; double-click opens settings | |
| 9 | Display scale | Settings > System > Display > Scale 100% → 150% → back: widget stays next to the tray, text crisp, no drift | |
| 10 | Resolution / monitor switch | Change resolution or unplug/plug the external monitor: widget re-anchors within 1 s | |
| 11 | Explorer restart | `Stop-Process -Name explorer -Force`: widget returns once the taskbar is back | |
| 12 | Auto-hide | Enable taskbar auto-hide: widget hides with the taskbar and follows it back when revealed | |
| 13 | Fullscreen | Play a fullscreen video / F11 browser: widget hides; leaving fullscreen brings it back | |
| 14 | Tray growth | Start an app that adds a tray icon (or toggle "show hidden icons"): widget shifts left immediately | |
| 15 | Settings live | Change preset, colours, font size, rows: widget updates instantly and stays right-anchored | |
| 16 | Persistence | Close settings, exit app, relaunch: settings kept | |
| 17 | Expired token | `CLAUDE_CONFIG_DIR` pointing at a fixture with `expiresAt: 1`: rows dim and show `↻ run claude`; Account card shows "Expired" | |
| 18 | No credentials | Delete the fixture file while running: widget shows `Sign in with claude` within 2 s; recreate it: rows return | |
| 19 | Network loss | Disable Wi-Fi for 2 minutes: numbers stay, stale dot appears; re-enable: dot disappears on next fetch | |
| 20 | Sleep / resume | Sleep the machine, wake it: widget is placed correctly and refreshes within a few seconds | |
| 21 | Second launch | Running the exe again opens settings instead of a second widget | |
| 22 | Startup | Reboot: widget is present after sign-in (Run at startup on) | |
| 23 | Logs | `%LOCALAPPDATA%\ClaudeToolbar\logs\app.log` contains no ERROR lines and no token | |
```

- [ ] **Step 2: Write the README**

`README.md`:
```markdown
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

1. Download `ClaudeToolbar.exe` from the latest build artifact (Actions → build → ClaudeToolbar-win-x64) or build it yourself (below).
2. Run it. The widget appears in the taskbar and an icon appears in the tray. "Run at startup" is on by default; turn it off from the menu or settings.
3. Make sure you have signed in to Claude Code at least once on this machine (`claude` in a terminal).

## Settings

Open from the widget, the tray icon, or by launching the exe a second time.

- Appearance: presets (Dark, Light, Claude, Mono), colours for background, text, bar track and the three bar levels, font size, corner radius, warning/critical thresholds.
- Rows: which windows to show, and whether to show the label, bar, percent and time.
- Behaviour: refresh interval (30–300 s), gap from the tray, hide when a fullscreen app is active, run at startup.
- Account: which credentials file is in use and the login state.

Settings live in `%APPDATA%\ClaudeToolbar\settings.json`. Logs live in `%LOCALAPPDATA%\ClaudeToolbar\logs\app.log`.

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
```

- [ ] **Step 3: Release build and full checklist**

```bash
dotnet test
dotnet publish src/ClaudeToolbar.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
ls -la publish/ClaudeToolbar.exe
```
Expected: all tests pass; a single exe of roughly 60–90 MB. Start `publish\ClaudeToolbar.exe`, run every row of `docs/verification.md` that can be done without a human (1–5, 11, 14 via tray toggle, 15–19, 21, 23), fill in the Result column, and leave the rest marked "needs human". Stop the app afterwards.

- [ ] **Step 4: Commit, push, confirm CI**

```bash
git add -A
git commit -m "Add README and verification checklist"
git push origin main
gh run list --limit 1
gh run watch "$(gh run list --limit 1 --json databaseId -q '.[0].databaseId')" --exit-status
```
Expected: the latest `build` run finishes green. If it fails, read the log with `gh run view --log-failed`, fix, commit with a plain message, and push again.

---

## Self-review notes

- Spec §4.1 credentials → Task 4; §4.2 endpoint/headers/shape → Tasks 3, 5; §4.3 scheduler → Tasks 7, 8; §4.4 formatting → Task 2; §5.1 window → Task 12; §5.2 positioning → Tasks 9, 11, 13; §5.3 interaction → Tasks 10, 14; §5.4 states → Tasks 9, 14; §6 settings window → Task 15; §6.1 settings file → Task 6; §7 error handling → Tasks 8, 10, 14; §8 tests → Tasks 2–9, 16; §9 repo/CI → Tasks 1, 10, 16; §10 limitations → README (Task 16).
- Deviation from spec, deliberate: `UsageWindow.ResetsAt` is nullable (the API returns `null` for an idle window) and the tray icon is drawn by a script into a real `.ico` instead of a hand-made asset.
- The "no credentials directory yet" case is covered by a 30 s retry in `App.Tick` because a `FileSystemWatcher` cannot watch a folder that does not exist.
