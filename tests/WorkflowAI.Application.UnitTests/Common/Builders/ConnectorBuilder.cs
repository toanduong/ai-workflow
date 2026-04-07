using WorkflowAI.Domain.Connectors;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Application.UnitTests.Common.Builders;

internal sealed class ConnectorBuilder
{
    private string _name = "Test Connector";
    private ConnectorType _connectorType = ConnectorType.Slack;
    private AuthModel _authModel = AuthModel.APIKey;
    private UserId _userId = UserId.New();
    private Guid? _credentialId;
    private string? _apiConnectionId;
    private bool _activate;
    private DateTime? _expiresAt;

    public ConnectorBuilder WithName(string name) { _name = name; return this; }
    public ConnectorBuilder WithType(ConnectorType type) { _connectorType = type; return this; }
    public ConnectorBuilder WithAuthModel(AuthModel authModel) { _authModel = authModel; return this; }
    public ConnectorBuilder WithUserId(UserId userId) { _userId = userId; return this; }
    public ConnectorBuilder WithCredentialId(Guid credentialId) { _credentialId = credentialId; return this; }
    public ConnectorBuilder WithApiConnectionId(string id) { _apiConnectionId = id; return this; }
    public ConnectorBuilder Activated(DateTime? expiresAt = null) { _activate = true; _expiresAt = expiresAt; return this; }

    public Connector Build()
    {
        var connector = Connector.Create(_name, _connectorType, _authModel, _userId);

        if (_credentialId.HasValue)
            connector.SetCredential(_credentialId.Value);

        if (_apiConnectionId is not null)
            connector.SetApiConnectionId(_apiConnectionId);

        if (_activate)
            connector.Activate(_expiresAt);

        return connector;
    }
}
