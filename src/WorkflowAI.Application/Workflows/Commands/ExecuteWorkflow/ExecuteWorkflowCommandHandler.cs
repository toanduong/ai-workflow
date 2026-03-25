using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.Workflows.Commands.ExecuteWorkflow;

public sealed class ExecuteWorkflowCommandHandler(
    IWorkflowRepository workflowRepository,
    IExecutionRepository executionRepository,
    IDomainEventDispatcher eventDispatcher,
    ICurrentUserService currentUserService)
    : IRequestHandler<ExecuteWorkflowCommand, Result<ExecutionId>>
{
    public async Task<Result<ExecutionId>> Handle(ExecuteWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workflow = await workflowRepository.GetByIdAsync(
            WorkflowId.From(request.WorkflowId), cancellationToken);

        if (workflow is null)
            return Error.NotFound("Workflow.NotFound", $"Workflow {request.WorkflowId} not found.");

        if (workflow.Status != WorkflowStatus.Active)
            return Error.Validation("Workflow.NotActive", "Only active workflows can be executed.");

        var triggeredBy = currentUserService.DisplayName ?? "System";
        var execution = WorkflowExecution.Create(workflow.Id, triggeredBy, request.InputData);

        foreach (var step in workflow.Steps.OrderBy(s => s.OrderIndex))
        {
            execution.AddStep(step.Id);
        }

        await executionRepository.AddAsync(execution, cancellationToken);
        await eventDispatcher.DispatchEventsAsync(execution, cancellationToken);

        return execution.Id;
    }
}
