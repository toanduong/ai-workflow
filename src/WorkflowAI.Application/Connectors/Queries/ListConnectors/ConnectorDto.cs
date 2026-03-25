namespace WorkflowAI.Application.Connectors.Queries.ListConnectors;

public sealed record ConnectorDto(
    Guid Id, string Name, string ConnectorType, string AuthModel,
    string Status, string? AzureApiConnectionId, string? Configuration,
    DateTime CreatedAt, DateTime? ExpiresAt);
