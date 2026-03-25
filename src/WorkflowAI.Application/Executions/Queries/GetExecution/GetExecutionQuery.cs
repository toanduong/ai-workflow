using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Executions.Queries.GetExecution;

public sealed record GetExecutionQuery(Guid ExecutionId) : IRequest<Result<ExecutionDto>>;
