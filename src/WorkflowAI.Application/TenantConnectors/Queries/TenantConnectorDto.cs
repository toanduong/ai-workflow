namespace WorkflowAI.Application.TenantConnectors.Queries;

public sealed record TenantConnectorDto(
    Guid Id,
    Guid TenantId,
    string ConnectorName,
    string Metadata,
    string Info,
    string Status,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record TenantConnectorApiDto(
    Guid Id,
    Guid TenantConnectorId,
    string ConnectorName,
    string ApiName,
    string HttpMethod,
    string UrlTemplate,
    string Metadata,
    DateTime CreatedAt
);
