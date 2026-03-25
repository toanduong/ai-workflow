using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Workflows.Queries.GetWorkflow;

public sealed record GetWorkflowQuery(Guid WorkflowId) : IRequest<Result<WorkflowDto>>;
