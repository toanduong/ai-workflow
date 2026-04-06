using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.UnitTests.Common.Builders;

internal sealed class TenantConnectorBuilder
{
    private TenantId _tenantId = TenantId.New();
    private string _connectorName = "Apollo";
    private string _metadata = """{"authType":"APIKey","requiredFields":["api_key"],"testEndpoint":{"method":"GET","path":"https://api.apollo.io/v1/auth/health"},"endpoints":{},"configSchema":{}}""";
    private string _info = """{"description":"Apollo.io sales intelligence platform","docsUrl":"https://apolloio.github.io/apollo-api-docs/","capabilities":["contact_search","email_sequences"],"rateLimits":"50 requests/minute","webhookSupport":true}""";
    private bool _activate;
    private bool _markFailed;
    private string _failureReason = "Connection refused";

    public TenantConnectorBuilder WithTenantId(TenantId tenantId) { _tenantId = tenantId; return this; }
    public TenantConnectorBuilder WithTenantId(Guid tenantId) { _tenantId = TenantId.From(tenantId); return this; }
    public TenantConnectorBuilder WithConnectorName(string name) { _connectorName = name; return this; }
    public TenantConnectorBuilder WithMetadata(string metadata, string info) { _metadata = metadata; _info = info; return this; }
    public TenantConnectorBuilder Activated() { _activate = true; return this; }
    public TenantConnectorBuilder Failed(string reason = "Connection refused") { _markFailed = true; _failureReason = reason; return this; }

    public TenantConnector Build()
    {
        var connector = TenantConnector.Create(_tenantId, _connectorName);
        connector.SetMetadata(_metadata, _info);

        if (_activate)
            connector.Activate();
        else if (_markFailed)
            connector.MarkFailed(_failureReason);

        return connector;
    }
}
