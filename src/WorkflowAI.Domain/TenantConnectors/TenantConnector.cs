using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors.Events;

namespace WorkflowAI.Domain.TenantConnectors;

public sealed class TenantConnector : AggregateRoot<TenantConnectorId>
{
    public TenantId TenantId { get; private set; }
    public string ConnectorName { get; private set; } = string.Empty;
    public string Metadata { get; private set; } = string.Empty;
    public string Info { get; private set; } = string.Empty;
    public TenantConnectorStatus Status { get; private set; } = TenantConnectorStatus.Pending;
    public string? FailureReason { get; private set; }

    private TenantConnector() { }

    public static TenantConnector Create(TenantId tenantId, string connectorName)
    {
        var connector = new TenantConnector
        {
            Id = TenantConnectorId.New(),
            TenantId = tenantId,
            ConnectorName = connectorName,
            Status = TenantConnectorStatus.Pending
        };
        connector.RaiseDomainEvent(new TenantConnectorProvisionedEvent(connector.Id));
        return connector;
    }

    public void SetMetadata(string metadata, string info)
    {
        Metadata = metadata;
        Info = info;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        Status = TenantConnectorStatus.Active;
        FailureReason = null;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new TenantConnectorActivatedEvent(Id));
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
