using ClaudeToolbar.Core.Mascot;
using ClaudeToolbar.Core.Sessions;

namespace ClaudeToolbar.Core.Tests.Sessions;

public class SessionTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private static SessionEvent Ev(SessionEventKind kind, string id = "s1", string? cwd = "C:\\work\\my-repo", string? message = null, string? detail = null) =>
        new(kind, id, cwd, message, detail);

    [Fact]
    public void StartCreatesIdleSessionNamedAfterTheFolder()
    {
        var t = new SessionTracker();
        Assert.Null(t.Apply(Ev(SessionEventKind.Start), T0));
        var s = Assert.Single(t.Sessions);
        Assert.Equal(("s1", "my-repo", SessionState.Idle), (s.Id, s.Name, s.State));
        Assert.Equal(MascotBadge.None, t.Badge);
        Assert.Equal("1 session · idle (my-repo)", t.Summary);
    }

    [Fact]
    public void NameFallsBackToTheSessionIdPrefix()
    {
        var t = new SessionTracker();
        t.Apply(Ev(SessionEventKind.Start, id: "0123456789abcdef", cwd: null), T0);
        Assert.Equal("01234567", t.Sessions[0].Name);
        t.Apply(Ev(SessionEventKind.Start, id: "0123456789abcdef", cwd: "/home/u/proj/"), T0);
        Assert.Equal("proj", t.Sessions[0].Name);
    }

    [Fact]
    public void PromptThenStopCuesFinishedOnce()
    {
        var t = new SessionTracker();
        Assert.Null(t.Apply(Ev(SessionEventKind.PromptSubmitted), T0));
        Assert.Equal(SessionState.Working, t.Sessions[0].State);
        Assert.Equal(MascotBadge.Working, t.Badge);
        Assert.Equal(SessionCue.Finished, t.Apply(Ev(SessionEventKind.Stopped), T0.AddMinutes(1)));
        Assert.Equal(MascotBadge.Finished, t.Badge);
        Assert.Null(t.Apply(Ev(SessionEventKind.Stopped), T0.AddMinutes(2)));
        Assert.Equal("1 session · finished (my-repo)", t.Summary);
    }

    [Fact]
    public void AttentionCuesOnceUntilTheStateChanges()
    {
        var t = new SessionTracker();
        Assert.Equal(SessionCue.Attention, t.Apply(Ev(SessionEventKind.NeedsAttention, message: "Permission needed"), T0));
        Assert.Equal("Permission needed", t.Sessions[0].Message);
        Assert.Null(t.Apply(Ev(SessionEventKind.NeedsAttention, message: "Still waiting"), T0.AddSeconds(30)));
        Assert.Equal(MascotBadge.Attention, t.Badge);
        t.Apply(Ev(SessionEventKind.PromptSubmitted), T0.AddMinutes(1));
        Assert.Equal(SessionCue.Attention, t.Apply(Ev(SessionEventKind.NeedsAttention), T0.AddMinutes(2)));
    }

    [Fact]
    public void FailedAlwaysCuesAndCarriesTheErrorType()
    {
        var t = new SessionTracker();
        Assert.Equal(SessionCue.Failed, t.Apply(Ev(SessionEventKind.Failed, detail: "rate_limit"), T0));
        Assert.Equal(SessionCue.Failed, t.Apply(Ev(SessionEventKind.Failed, detail: "rate_limit"), T0));
        Assert.Equal(MascotBadge.Failed, t.Badge);
        Assert.Equal("rate_limit", t.Sessions[0].Message);
    }

    [Fact]
    public void InfoNeverChangesStateButRegistersNewSessions()
    {
        var t = new SessionTracker();
        t.Apply(Ev(SessionEventKind.PromptSubmitted), T0);
        Assert.Null(t.Apply(Ev(SessionEventKind.Info), T0));
        Assert.Equal(SessionState.Working, t.Sessions[0].State);
        Assert.Null(t.Apply(Ev(SessionEventKind.Info, id: "s2"), T0));
        Assert.Equal(2, t.Sessions.Count);
    }

    [Fact]
    public void EndedRemovesTheSession()
    {
        var t = new SessionTracker();
        t.Apply(Ev(SessionEventKind.PromptSubmitted), T0);
        Assert.Null(t.Apply(Ev(SessionEventKind.Ended), T0));
        Assert.Empty(t.Sessions);
        Assert.Null(t.Summary);
    }

    [Fact]
    public void BadgePrecedenceAndSummaryAcrossSessions()
    {
        var t = new SessionTracker();
        t.Apply(Ev(SessionEventKind.PromptSubmitted, id: "a", cwd: "/x/web"), T0);
        t.Apply(Ev(SessionEventKind.Stopped, id: "b", cwd: "/x/api"), T0.AddSeconds(1));
        t.Apply(Ev(SessionEventKind.NeedsAttention, id: "c", cwd: "/x/cli"), T0.AddSeconds(2));
        Assert.Equal(MascotBadge.Attention, t.Badge);
        Assert.Equal("3 sessions · 1 needs you (cli) · 1 finished (api) · 1 working (web)", t.Summary);
        Assert.Equal(["c", "b", "a"], t.Sessions.Select(s => s.Id).ToList());
    }

    [Fact]
    public void AcknowledgeClearsFinishedAndFailedAndAssumesAttentionWasHandled()
    {
        var t = new SessionTracker();
        t.Apply(Ev(SessionEventKind.Stopped, id: "a"), T0);
        t.Apply(Ev(SessionEventKind.Failed, id: "b"), T0);
        t.Apply(Ev(SessionEventKind.NeedsAttention, id: "c", message: "m"), T0);
        t.Acknowledge();
        var states = t.Sessions.ToDictionary(s => s.Id, s => s.State);
        Assert.Equal(SessionState.Idle, states["a"]);
        Assert.Equal(SessionState.Idle, states["b"]);
        Assert.Equal(SessionState.Working, states["c"]);
        Assert.All(t.Sessions, s => Assert.Null(s.Message));
        Assert.Equal(MascotBadge.Working, t.Badge);
    }

    [Fact]
    public void PruneDropsStaleSessions()
    {
        var t = new SessionTracker();
        t.Apply(Ev(SessionEventKind.PromptSubmitted, id: "old"), T0);
        t.Apply(Ev(SessionEventKind.PromptSubmitted, id: "new"), T0.AddHours(11));
        t.Prune(T0.AddHours(12).AddMinutes(1));
        Assert.Equal(["new"], t.Sessions.Select(s => s.Id).ToList());
        Assert.Equal(TimeSpan.FromHours(12), SessionTracker.StaleAfter);
    }
}
