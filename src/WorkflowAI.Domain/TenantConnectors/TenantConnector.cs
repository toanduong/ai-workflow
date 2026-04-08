using System.Text.Json;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors.Events;

namespace WorkflowAI.Domain.TenantConnectors;

/// <summary>
/// Table 1: TenantConnectors
/// One row per tenant+connector pair (e.g. Tenant ABC + Odoo).
/// Metadata and Info are Claude-generated JSON describing the 3rd-party integration.
/// </summary>
public sealed class TenantConnector : AggregateRoot<TenantConnectorId>
{
    public TenantId TenantId { get; private set; }
    public string ConnectorType { get; private set; } = string.Empty;
    public string Metadata { get; private set; } = string.Empty;       // Claude-generated: authType, requiredFields, baseUrl, testEndpoint, configSchema
    public string Info { get; private set; } = string.Empty;           // Claude-generated: description, docsUrl, capabilities, rateLimits
    public TenantConnectorStatus Status { get; private set; } = TenantConnectorStatus.Pending;
    public string? FailureReason { get; private set; }
    public int Version { get; private set; } = 1;

    /// <summary>
    /// JSON dict mapping each credential field name → its Key Vault secret name.
    /// e.g. { "api_key": "connector-{id}-api_key", "instance_url": "connector-{id}-instance_url" }
    /// Null until credentials are successfully stored after validation.
    /// At runtime, workflow steps read this to resolve the actual secrets from Key Vault.
    /// </summary>
    public string? CredentialSecretNames { get; private set; }

    private TenantConnector() { }

    public static TenantConnector Create(TenantId tenantId, string connectorType)
    {
        var connector = new TenantConnector
        {
            Id = TenantConnectorId.New(),
            TenantId = tenantId,
            ConnectorType = connectorType,
            Status = TenantConnectorStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        connector.RaiseDomainEvent(new TenantConnectorProvisionedEvent(connector.Id, tenantId, connectorType));
        return connector;
    }

    public void SetMetadata(string metadata, string info)
    {
        Metadata = metadata;
        Info = info;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the connector as Active and stores the Key Vault secret name mapping.
    /// credentialSecretNames keys match Claude's requiredFields; values are Key Vault secret names.
    /// </summary>
    public void Activate(IReadOnlyDictionary<string, string>? credentialSecretNames = null)
    {
        Status = TenantConnectorStatus.Active;
        FailureReason = null;
        if (credentialSecretNames is { Count: > 0 })
            CredentialSecretNames = JsonSerializer.Serialize(credentialSecretNames);
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new TenantConnectorActivatedEvent(Id, TenantId, ConnectorType));
    }

    public void MarkFailed(string reason)
    {
        Status = TenantConnectorStatus.Failed;
        FailureReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Suspend()
    {
        Status = TenantConnectorStatus.Suspended;
        UpdatedAt = DateTime.UtcNow;
    }
}
