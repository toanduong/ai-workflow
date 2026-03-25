using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Executions;

namespace WorkflowAI.Application.Executions.Queries.GetExecution;

public sealed class GetExecutionQueryHandler(IExecutionRepository executionRepository)
    : IRequestHandler<GetExecutionQuery, Result<ExecutionDto>>
{
    public async Task<Result<ExecutionDto>> Handle(GetExecutionQuery request, CancellationToken ct)
    {
        var execution = await executionRepository.GetByIdAsync(ExecutionId.From(request.ExecutionId), ct);
        if (execution is null)
            return Error.NotFound("Execution.NotFound", "Execution not found.");

        var stepDtos = execution.Steps.Select(s => new StepExecutionDto(
            s.Id, s.WorkflowStepId, s.Status.Name, s.InputData, s.OutputData,
            s.StartedAt, s.CompletedAt, s.ErrorMessage)).ToList().AsReadOnly();

        return new ExecutionDto(
            execution.Id.Value, execution.WorkflowId.Value, execution.Status.Name,
            execution.InputData, execution.OutputData, execution.StartedAt,
            execution.CompletedAt, execution.TriggeredBy, stepDtos);
    }
}
