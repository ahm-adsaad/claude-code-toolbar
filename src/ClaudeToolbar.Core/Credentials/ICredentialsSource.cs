namespace ClaudeToolbar.Core.Credentials;

public interface ICredentialsSource
{
    string Path { get; }
    CredentialsState Read();
}
