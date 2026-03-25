using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Connectors;

public sealed class CredentialType : Enumeration<CredentialType>
{
    public static readonly CredentialType AccessToken = new(1, nameof(AccessToken));
    public static readonly CredentialType RefreshToken = new(2, nameof(RefreshToken));
    public static readonly CredentialType APIKey = new(3, nameof(APIKey));
    public static readonly CredentialType ConnectionString = new(4, nameof(ConnectionString));
    public static readonly CredentialType ClientSecret = new(5, nameof(ClientSecret));

    private CredentialType(int id, string name) : base(id, name) { }
}
