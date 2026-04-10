using MediatR;

namespace WorkflowAI.Application.TenantConnectors.Queries.ListConnectorApis;

/// <summary>
/// Query to list all generated APIs for a specific tenant connector.
/// </summary>
public sealed record ListConnectorApisQuery(Guid TenantConnectorId) : IRequest<ListConnectorApisResult>;

/// <summary>
/// Result containing the list of APIs for a connector.
/// </summary>
public sealed record ListConnectorApisResult(
    bool IsSuccess,
    List<ConnectorApiDto>? Apis,
    string? Error);

/// <summary>
/// DTO for a connector API.
/// </summary>
public sealed record ConnectorApiDto(
    Guid Id,
    string ApiName,
    string HttpMethod,
    string UrlTemplate,
    string? Metadata,
    int Version,
    DateTime CreatedAt);
