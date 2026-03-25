using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Connectors;

public sealed class AuthModel : Enumeration<AuthModel>
{
    public static readonly AuthModel OAuth2 = new(1, nameof(OAuth2));
    public static readonly AuthModel ServicePrincipal = new(2, nameof(ServicePrincipal));
    public static readonly AuthModel APIKey = new(3, nameof(APIKey));
    public static readonly AuthModel ManagedIdentity = new(4, nameof(ManagedIdentity));
    public static readonly AuthModel ConnectionString = new(5, nameof(ConnectionString));

    private AuthModel(int id, string name) : base(id, name) { }
}
