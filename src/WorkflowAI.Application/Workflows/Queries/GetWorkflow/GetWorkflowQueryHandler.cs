using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.Workflows.Queries.GetWorkflow;

public sealed class GetWorkflowQueryHandler(
    IWorkflowRepository workflowRepository)
    : IRequestHandler<GetWorkflowQuery, Result<WorkflowDto>>
{
    public async Task<Result<WorkflowDto>> Handle(GetWorkflowQuery request, CancellationToken cancellationToken)
    {
        var workflow = await workflowRepository.GetByIdAsync(
            WorkflowId.From(request.WorkflowId), cancellationToken);

        if (workflow is null)
            return Error.NotFound("Workflow.NotFound", $"Workflow {request.WorkflowId} not found.");

        var dto = new WorkflowDto(
            workflow.Id.Value,
            workflow.Name,
            workflow.Description,
            workflow.Status.Name,
            workflow.Steps.Select(s => new WorkflowStepDto(
                s.Id,
                s.OrderIndex,
                s.Name,
                s.StepType.Name,
                s.Configuration,
                s.RequiredRole,
                s.TimeoutMinutes,
                s.OnTimeoutAction.Name)).ToList(),
            workflow.CreatedAt,
            workflow.LogicAppResourceId);

        return dto;
    }
}
