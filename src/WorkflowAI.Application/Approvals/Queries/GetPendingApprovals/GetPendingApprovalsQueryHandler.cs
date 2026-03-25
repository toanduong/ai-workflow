using MediatR;
using WorkflowAI.Domain.Approvals;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Approvals.Queries.GetPendingApprovals;

public sealed class GetPendingApprovalsQueryHandler(IApprovalRepository approvalRepository)
    : IRequestHandler<GetPendingApprovalsQuery, Result<IReadOnlyList<ApprovalDto>>>
{
    public async Task<Result<IReadOnlyList<ApprovalDto>>> Handle(GetPendingApprovalsQuery request, CancellationToken ct)
    {
        var approvals = await approvalRepository.GetPendingAsync(ct);
        var dtos = approvals.Select(a => new ApprovalDto(
            a.Id.Value, a.StepExecutionId, a.Title, a.Description, a.Status.Name,
            a.ExpiresAt, a.CreatedAt)).ToList().AsReadOnly();
        return Result<IReadOnlyList<ApprovalDto>>.Success(dtos);
    }
}
