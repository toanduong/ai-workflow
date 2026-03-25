using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Executions;

namespace WorkflowAI.Application.Executions.Commands.StartExecution;

public sealed record StartExecutionCommand(Guid WorkflowId, Guid ExecutionId) : IRequest<Result>;
