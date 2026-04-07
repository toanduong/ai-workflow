using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.TenantConnectors.Events;

public sealed record TenantConnectorProvisionedEvent(
    TenantConnectorId TenantConnectorId,
    TenantId TenantId,
    string ConnectorName) : IDomainEvent;
