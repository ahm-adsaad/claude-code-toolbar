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
