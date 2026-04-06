namespace WorkflowAI.Application.TenantConnectors.Queries;

public sealed record TenantConnectorDto(
    Guid Id,
    Guid TenantId,
    string ConnectorName,
    string Metadata,
    string Info,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
