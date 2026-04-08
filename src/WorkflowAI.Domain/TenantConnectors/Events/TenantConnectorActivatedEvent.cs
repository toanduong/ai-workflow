using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.TenantConnectors.Events;

public sealed record TenantConnectorActivatedEvent(
    TenantConnectorId TenantConnectorId,
    TenantId TenantId,
    string ConnectorType) : IDomainEvent;
