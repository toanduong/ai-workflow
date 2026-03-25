using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.Workflows.Queries.ListWorkflows;

public sealed class ListWorkflowsQueryHandler(
    IWorkflowRepository workflowRepository)
    : IRequestHandler<ListWorkflowsQuery, Result<IReadOnlyList<WorkflowSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<WorkflowSummaryDto>>> Handle(
        ListWorkflowsQuery request, CancellationToken cancellationToken)
    {
        var workflows = await workflowRepository.GetAllAsync(cancellationToken);

        var dtos = workflows.Select(w => new WorkflowSummaryDto(
            w.Id.Value,
            w.Name,
            w.Description,
            w.Status.Name,
            w.Steps.Count,
            w.CreatedAt)).ToList();

        return dtos;
    }
}
