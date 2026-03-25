using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.Executions.Commands.AdvanceStep;

public sealed class AdvanceStepCommandHandler(
    IWorkflowRepository workflowRepository,
    IExecutionRepository executionRepository,
    IDomainEventDispatcher eventDispatcher)
    : IRequestHandler<AdvanceStepCommand, Result>
{
    public async Task<Result> Handle(AdvanceStepCommand request, CancellationToken ct)
    {
        var execution = await executionRepository.GetByIdAsync(ExecutionId.From(request.ExecutionId), ct);
        if (execution is null)
            return Error.NotFound("Execution.NotFound", "Execution not found.");

        var workflow = await workflowRepository.GetByIdAsync(execution.WorkflowId, ct);
        if (workflow is null)
            return Error.NotFound("Workflow.NotFound", "Workflow not found.");

        var steps = workflow.Steps.OrderBy(s => s.OrderIndex).ToList();
        var currentIndex = steps.FindIndex(s => s.Id == request.CompletedStepId);

        if (currentIndex < 0)
            return Error.NotFound("Step.NotFound", "Step not found in workflow.");

        if (currentIndex + 1 < steps.Count)
        {
            var nextStep = steps[currentIndex + 1];
            execution.AddStep(nextStep.Id);
        }
        else
        {
            execution.Complete();
        }

        await executionRepository.UpdateAsync(execution, ct);
        await eventDispatcher.DispatchEventsAsync(execution, ct);
        return Result.Success();
    }
}
