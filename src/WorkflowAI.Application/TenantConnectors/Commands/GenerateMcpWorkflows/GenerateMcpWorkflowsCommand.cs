using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.TenantConnectors.Commands.GenerateMcpWorkflows;

public sealed record GenerateMcpWorkflowsCommand(
    Guid TenantConnectorId) : IRequest<Result<GenerateMcpWorkflowsResult>>;

public sealed record GenerateMcpWorkflowsResult(
    string ApiRoutes,
    string WorkflowDefs);
