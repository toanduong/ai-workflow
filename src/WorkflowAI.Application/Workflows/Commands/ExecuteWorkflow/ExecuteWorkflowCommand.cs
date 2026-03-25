using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Executions;

namespace WorkflowAI.Application.Workflows.Commands.ExecuteWorkflow;

public sealed record ExecuteWorkflowCommand(Guid WorkflowId, string? InputData = null) : IRequest<Result<ExecutionId>>;
