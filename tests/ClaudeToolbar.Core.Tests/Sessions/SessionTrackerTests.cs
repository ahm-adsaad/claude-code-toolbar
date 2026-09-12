using ClaudeToolbar.Core.Mascot;
using ClaudeToolbar.Core.Sessions;

namespace ClaudeToolbar.Core.Tests.Sessions;

public class SessionTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private static SessionEvent Ev(SessionEventKind kind, string id = "s1", string? cwd = "C:\\work\\my-repo", string? message = null, string? detail = null, int agents = 0) =>
        new(kind, id, cwd, message, detail, agents);

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
        t.Apply(Ev(SessionEventKind.Start, id: "0123456789abcdef", cwd: " /x/y "), T0);
        Assert.Equal("y", t.Sessions[0].Name);
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
    public void StopWithAgentsStillRunningKeepsWorkingUntilTheLastOneIsDone()
    {
        var t = new SessionTracker();
        t.Apply(Ev(SessionEventKind.PromptSubmitted), T0);
        // The turn ended but two background agents are still going: no cue, still working, and the line says so.
        Assert.Null(t.Apply(Ev(SessionEventKind.Stopped, agents: 2), T0.AddMinutes(1)));
        Assert.Equal((SessionState.Working, 2), (t.Sessions[0].State, t.Sessions[0].Agents));
        Assert.Equal(MascotBadge.Working, t.Badge);
        Assert.Equal("1 session · working (my-repo)", t.Summary);
        Assert.Equal("my-repo · working · 2 agents · now", SessionLine.Text(t.Sessions[0], T0.AddMinutes(1)));
        // An agent finished and the main agent picked up its result, then stopped again with one still running.
        Assert.Null(t.Apply(Ev(SessionEventKind.Stopped, agents: 1), T0.AddMinutes(2)));
        Assert.Equal("my-repo · working · 1 agent · now", SessionLine.Text(t.Sessions[0], T0.AddMinutes(2)));
        // Events in between keep the count; a Stop with nobody left is the real finish.
        t.Apply(Ev(SessionEventKind.Info), T0.AddMinutes(3));
        Assert.Equal(1, t.Sessions[0].Agents);
        Assert.Equal(SessionCue.Finished, t.Apply(Ev(SessionEventKind.Stopped), T0.AddMinutes(4)));
        Assert.Equal((SessionState.Finished, 0), (t.Sessions[0].State, t.Sessions[0].Agents));
    }

    [Fact]
    public void StopWithAgentsRunningDemotesAttentionAndSurvivesAcknowledge()
    {
        var t = new SessionTracker();
        t.Apply(Ev(SessionEventKind.NeedsAttention, message: "permission"), T0);
        Assert.Null(t.Apply(Ev(SessionEventKind.Stopped, agents: 1), T0.AddMinutes(1)));
        Assert.Equal(SessionState.Working, t.Sessions[0].State);
        Assert.Null(t.Sessions[0].Message);
        t.Acknowledge();
        Assert.Equal((SessionState.Working, 1), (t.Sessions[0].State, t.Sessions[0].Agents));
        // A new prompt while the agent runs keeps the count until the next Stop reports it.
        t.Apply(Ev(SessionEventKind.PromptSubmitted), T0.AddMinutes(2));
        Assert.Equal(1, t.Sessions[0].Agents);
        // A finished session that is acknowledged and then stops again with agents goes back to working.
        t.Apply(Ev(SessionEventKind.Stopped), T0.AddMinutes(3));
        t.Acknowledge();
        Assert.Equal(SessionState.Idle, t.Sessions[0].State);
        Assert.Null(t.Apply(Ev(SessionEventKind.Stopped, agents: 3), T0.AddMinutes(4)));
        Assert.Equal((SessionState.Working, 3), (t.Sessions[0].State, t.Sessions[0].Agents));
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

    [Fact]
    public void ApplyKeepsTheNewestHostAndIgnoresNull()
    {
        var t = new SessionTracker();
        var wt = new SessionHost(70, "Windows Terminal", T0);
        t.Apply(Ev(SessionEventKind.Start), T0, wt);
        t.Apply(Ev(SessionEventKind.PromptSubmitted), T0.AddMinutes(1));
        Assert.Equal(wt, t.Sessions[0].Host);
        var code = new SessionHost(80, "VS Code", T0.AddMinutes(2));
        t.Apply(Ev(SessionEventKind.Stopped), T0.AddMinutes(2), code);
        Assert.Equal(code, t.Session("s1")!.Host);
        t.Acknowledge();
        Assert.Equal(code, t.Session("s1")!.Host);
        Assert.Null(t.Session("missing"));
    }

    [Fact]
    public void JumpCandidatePrefersTheMostUrgentThenTheNewest()
    {
        var t = new SessionTracker();
        var host = new SessionHost(70, "Windows Terminal", T0);
        t.Apply(Ev(SessionEventKind.Stopped, id: "old-finished", cwd: "C:\\a"), T0, host);
        t.Apply(Ev(SessionEventKind.NeedsAttention, id: "waiting", cwd: "C:\\b", message: "permission"), T0.AddMinutes(-5), host);
        t.Apply(Ev(SessionEventKind.PromptSubmitted, id: "busy", cwd: "C:\\c"), T0.AddMinutes(1), host);
        Assert.Equal("waiting", t.JumpCandidate!.Id);
        t.Acknowledge();   // waiting → working (older), old-finished → idle
        Assert.Equal("busy", t.JumpCandidate!.Id);
        t.Apply(Ev(SessionEventKind.PromptSubmitted, id: "waiting", cwd: "C:\\b"), T0.AddMinutes(2), host);
        Assert.Equal("waiting", t.JumpCandidate!.Id);
    }

    [Fact]
    public void JumpCandidateSkipsSessionsWithoutAHost()
    {
        var t = new SessionTracker();
        Assert.Null(t.JumpCandidate);
        t.Apply(Ev(SessionEventKind.NeedsAttention, id: "nohost", message: "x"), T0);
        Assert.Null(t.JumpCandidate);
        t.Apply(Ev(SessionEventKind.PromptSubmitted, id: "hosted", cwd: "C:\\h"), T0.AddMinutes(-1), new SessionHost(1, "Console", T0));
        Assert.Equal("hosted", t.JumpCandidate!.Id);
    }

    [Fact]
    public void SessionLineShowsNameStateHostAndAge()
    {
        var host = new SessionHost(80, "VS Code", T0);
        var s = new SessionInfo("s1", "api", SessionState.NeedsAttention, T0, "permission", host);
        Assert.Equal("api · needs you · VS Code · 2 min", SessionLine.Text(s, T0.AddMinutes(2)));
        Assert.Equal("api · needs you · VS Code · now", SessionLine.Text(s, T0.AddSeconds(30)));
        Assert.Equal("api · needs you · VS Code · 3 h", SessionLine.Text(s, T0.AddHours(3).AddMinutes(20)));
        Assert.Equal("api · working · now", SessionLine.Text(s with { State = SessionState.Working, Host = null }, T0));
        Assert.Equal("api · idle · now", SessionLine.Text(s with { State = SessionState.Idle, Host = null }, T0));
        Assert.Equal("api · failed · now", SessionLine.Text(s with { State = SessionState.Failed, Host = null }, T0));
        Assert.Equal("api · finished · now", SessionLine.Text(s with { State = SessionState.Finished, Host = null }, T0));
        Assert.Equal("api · working · 2 agents · VS Code · now", SessionLine.Text(s with { State = SessionState.Working, Agents = 2 }, T0));
        Assert.Equal("api · working · 1 agent · now", SessionLine.Text(s with { State = SessionState.Working, Host = null, Agents = 1 }, T0));
        // The count only belongs to a working session; a finished line never mentions agents.
        Assert.Equal("api · finished · now", SessionLine.Text(s with { State = SessionState.Finished, Host = null, Agents = 2 }, T0));
    }
}
