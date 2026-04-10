using System.Text.Json;
using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.TenantConnectors.Commands.ExecuteConnectorApi;

/// <summary>
/// Executes a generated 3rd-party API on behalf of the caller.
/// The backend resolves credentials from Key Vault and proxies the request —
/// callers never see the API key or the 3rd-party endpoint directly.
/// </summary>
public sealed record ExecuteConnectorApiCommand(
    Guid TenantId,
    Guid TenantConnectorId,
    Guid ApiId,
    JsonElement? RequestBody) : IRequest<Result<JsonElement>>;
