using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.TenantConnectors.Commands.GenerateMcpWorkflows;

public sealed record GenerateMcpWorkflowsCommand(
    Guid TenantConnectorId
) : IRequest<Result<GenerateMcpWorkflowsResult>>;

public sealed record GenerateMcpWorkflowsResult(
    string ConnectorType,
    int ApiRoutesCount,
    int WorkflowTemplatesCount,
    string ApiRoutes,       // JSON array of generated API route definitions
    string WorkflowDefs     // JSON array of generated workflow template definitions
);
