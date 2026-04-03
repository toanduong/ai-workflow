using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Executions.Commands.ExecuteHttpStep;

public sealed record ExecuteHttpStepCommand(Guid ExecutionId, Guid StepExecutionId) : IRequest<Result>;
