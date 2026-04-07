using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.TenantConnectors.Commands.GenerateConnectorAssets;

public sealed record GenerateConnectorAssetsCommand(
    Guid TenantConnectorId) : IRequest<Result<GenerateConnectorAssetsResult>>;

public sealed record GenerateConnectorAssetsResult(
    string ApiRoutes,
    string WorkflowDefs);
