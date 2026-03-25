using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.AIAgent.Commands.ExecuteAIStep;

public sealed record ExecuteAIStepCommand(Guid StepExecutionId, string? Configuration) : IRequest<Result>;
