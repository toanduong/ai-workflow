using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Approvals;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Approvals.Commands.HandleApprovalTimeout;

public sealed class HandleApprovalTimeoutCommandHandler(
    IApprovalRepository approvalRepository,
    IDomainEventDispatcher eventDispatcher)
    : IRequestHandler<HandleApprovalTimeoutCommand, Result<int>>
{
    public async Task<Result<int>> Handle(HandleApprovalTimeoutCommand request, CancellationToken ct)
    {
        var expired = await approvalRepository.GetExpiredPendingAsync(ct);
        var processed = 0;

        foreach (var approval in expired)
        {
            // Default to escalate for now; in full impl, read OnTimeoutAction from the step
            var result = approval.Escalate();
            if (result.IsSuccess)
            {
                await approvalRepository.UpdateAsync(approval, ct);
                await eventDispatcher.DispatchEventsAsync(approval, ct);
                processed++;
            }
        }

        return processed;
    }
}
