using ClaudeToolbar.Core.Sessions;

namespace ClaudeToolbar.Core.Tests.Sessions;

public class HostChainTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 8, 9, 0, 0, TimeSpan.Zero);

    // startOffsetSeconds: larger = started later. Children start after their parents.
    private static ProcessRecord P(int pid, int parent, string name, bool window = false, int startOffsetSeconds = 0) =>
        new(pid, parent, name, T0.AddSeconds(startOffsetSeconds), window);

    private static IReadOnlyDictionary<int, ProcessRecord> Table(params ProcessRecord[] records) => records.ToDictionary(r => r.Pid);

    [Fact]
    public void FindsWindowsTerminalThreeLevelsUp()
    {
        var table = Table(
            P(100, 90, "claude", startOffsetSeconds: 30),
            P(90, 80, "pwsh", startOffsetSeconds: 20),
            P(80, 70, "openconsole", startOffsetSeconds: 10),
            P(70, 1, "windowsterminal", window: true));
        Assert.Equal(new HostChainResult(70, "Windows Terminal", true), HostChain.Resolve(100, table, KnownHosts.Windows));
    }

    [Fact]
    public void TheClaudeCliIsNotTheDesktopApp()
    {
        var table = Table(P(100, 90, "claude", startOffsetSeconds: 10), P(90, 1, "windowsterminal", window: true));
        Assert.Equal("Windows Terminal", HostChain.Resolve(100, table, KnownHosts.Windows)!.DisplayName);
    }

    [Fact]
    public void TheDesktopAppCountsWhenItOwnsAWindow()
    {
        var table = Table(P(100, 50, "claude", startOffsetSeconds: 10), P(50, 1, "claude", window: true));
        Assert.Equal(new HostChainResult(50, "Claude", true), HostChain.Resolve(100, table, KnownHosts.Windows));
    }

    [Fact]
    public void VsCodeHelperWithoutAWindowIsSkippedForItsMainProcess()
    {
        var table = Table(
            P(100, 90, "claude", startOffsetSeconds: 30),
            P(90, 80, "pwsh", startOffsetSeconds: 20),
            P(80, 70, "code", startOffsetSeconds: 10),
            P(70, 1, "code", window: true));
        Assert.Equal(new HostChainResult(70, "VS Code", true), HostChain.Resolve(100, table, KnownHosts.Windows));
    }

    [Fact]
    public void APlainConsoleIsTheShellThatOwnsTheWindow()
    {
        var table = Table(P(100, 90, "claude", startOffsetSeconds: 10), P(90, 1, "pwsh", window: true));
        Assert.Equal(new HostChainResult(90, "Console", true), HostChain.Resolve(100, table, KnownHosts.Windows));
    }

    [Fact]
    public void UnknownHostFallsBackToTheFirstWindowedAncestor()
    {
        var table = Table(
            P(100, 90, "claude", startOffsetSeconds: 10),
            P(90, 80, "node", startOffsetSeconds: 5),
            P(80, 1, "myterm", window: true));
        Assert.Equal(new HostChainResult(80, "myterm", false), HostChain.Resolve(100, table, KnownHosts.Windows));
    }

    [Fact]
    public void AKnownHostBeatsAnEarlierUnknownWindowedAncestor()
    {
        var table = Table(
            P(100, 90, "claude", startOffsetSeconds: 20),
            P(90, 80, "helper", window: true, startOffsetSeconds: 10),
            P(80, 1, "windowsterminal", window: true));
        Assert.Equal("Windows Terminal", HostChain.Resolve(100, table, KnownHosts.Windows)!.DisplayName);
    }

    [Fact]
    public void StopsAtTheShellWithoutPickingIt()
    {
        var table = Table(P(100, 90, "claude", startOffsetSeconds: 10), P(90, 1, "explorer", window: true));
        Assert.Null(HostChain.Resolve(100, table, KnownHosts.Windows));
    }

    [Fact]
    public void MissingParentOrMissingStartStopsTheWalk()
    {
        Assert.Null(HostChain.Resolve(100, Table(P(100, 999, "claude")), KnownHosts.Windows));
        Assert.Null(HostChain.Resolve(5, Table(P(100, 1, "claude")), KnownHosts.Windows));
    }

    [Fact]
    public void AParentYoungerThanItsChildIsAReusedPid()
    {
        var table = Table(P(100, 90, "claude"), P(90, 1, "windowsterminal", window: true, startOffsetSeconds: 60));
        Assert.Null(HostChain.Resolve(100, table, KnownHosts.Windows));
    }

    [Fact]
    public void AProcessThatIsItsOwnParentStopsTheWalk()
    {
        Assert.Null(HostChain.Resolve(100, Table(P(100, 100, "claude")), KnownHosts.Windows));
    }

    [Fact]
    public void WalkIsCappedAtTwelveProcesses()
    {
        static IReadOnlyDictionary<int, ProcessRecord> Chain(int nodesBeforeHost)
        {
            var records = new List<ProcessRecord>();
            for (var i = 0; i < nodesBeforeHost; i++)
                records.Add(P(100 + i, 101 + i, "node", startOffsetSeconds: nodesBeforeHost - i));
            records.Add(P(100 + nodesBeforeHost, 1, "windowsterminal", window: true));
            return records.ToDictionary(r => r.Pid);
        }
        Assert.NotNull(HostChain.Resolve(100, Chain(HostChain.MaxDepth - 1), KnownHosts.Windows));
        Assert.Null(HostChain.Resolve(100, Chain(HostChain.MaxDepth), KnownHosts.Windows));
    }

    [Fact]
    public void CatalogsMapNamesAndStopWords()
    {
        Assert.Equal("Terminal", KnownHosts.Mac.DisplayName("com.apple.Terminal"));
        Assert.Equal("VS Code", KnownHosts.Mac.DisplayName("com.microsoft.VSCode"));
        Assert.Equal("JetBrains", KnownHosts.Mac.DisplayName("com.jetbrains.intellij"));
        Assert.Null(KnownHosts.Mac.DisplayName("com.example.other"));
        Assert.True(KnownHosts.Mac.StopsWalk("com.apple.finder"));
        Assert.False(KnownHosts.Mac.StopsWalk("com.apple.Terminal"));
        Assert.Equal("Git Bash", KnownHosts.Windows.DisplayName("mintty"));
        Assert.Equal("JetBrains", KnownHosts.Windows.DisplayName("rider64"));
        Assert.True(KnownHosts.Windows.StopsWalk("explorer"));
        Assert.False(KnownHosts.Windows.StopsWalk("code"));
    }
}
