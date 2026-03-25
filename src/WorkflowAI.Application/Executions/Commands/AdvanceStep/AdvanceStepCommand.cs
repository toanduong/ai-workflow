using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Executions.Commands.AdvanceStep;

public sealed record AdvanceStepCommand(Guid ExecutionId, Guid CompletedStepId) : IRequest<Result>;
