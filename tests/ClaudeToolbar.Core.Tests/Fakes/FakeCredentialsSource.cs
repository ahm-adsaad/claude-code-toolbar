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
