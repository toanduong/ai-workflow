using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Approvals.Queries.GetPendingApprovals;

public sealed record GetPendingApprovalsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<ApprovalDto>>>;
