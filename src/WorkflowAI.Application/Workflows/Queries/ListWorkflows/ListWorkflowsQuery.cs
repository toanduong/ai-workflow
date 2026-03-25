using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Workflows.Queries.ListWorkflows;

public sealed record ListWorkflowsQuery : IRequest<Result<IReadOnlyList<WorkflowSummaryDto>>>;

public sealed record WorkflowSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    string Status,
    int StepCount,
    DateTime CreatedAt);
