using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.Executions.Commands.StartExecution;

public sealed class StartExecutionCommandHandler(
    IWorkflowRepository workflowRepository,
    IExecutionRepository executionRepository)
    : IRequestHandler<StartExecutionCommand, Result>
{
    public async Task<Result> Handle(StartExecutionCommand request, CancellationToken ct)
    {
        var workflow = await workflowRepository.GetByIdAsync(WorkflowId.From(request.WorkflowId), ct);
        if (workflow is null)
            return Error.NotFound("Workflow.NotFound", "Workflow not found.");

        var execution = await executionRepository.GetByIdAsync(ExecutionId.From(request.ExecutionId), ct);
        if (execution is null)
            return Error.NotFound("Execution.NotFound", "Execution not found.");

        // Add first step to execution if workflow has steps
        if (workflow.Steps.Count > 0)
        {
            var firstStep = workflow.Steps.OrderBy(s => s.OrderIndex).First();
            execution.AddStep(firstStep.Id);
            await executionRepository.UpdateAsync(execution, ct);
        }

        return Result.Success();
    }
}
