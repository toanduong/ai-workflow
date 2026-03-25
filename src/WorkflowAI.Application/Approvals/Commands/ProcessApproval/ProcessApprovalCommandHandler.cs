using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Approvals;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Application.Approvals.Commands.ProcessApproval;

public sealed class ProcessApprovalCommandHandler(
    IApprovalRepository approvalRepository,
    IDomainEventDispatcher eventDispatcher)
    : IRequestHandler<ProcessApprovalCommand, Result>
{
    public async Task<Result> Handle(ProcessApprovalCommand request, CancellationToken ct)
    {
        var approval = await approvalRepository.GetByIdAsync(ApprovalRequestId.From(request.ApprovalRequestId), ct);
        if (approval is null)
            return Error.NotFound("Approval.NotFound", "Approval request not found.");

        var channel = ApprovalChannel.FromName(request.Channel);
        if (channel is null)
            return Error.Validation("Approval.InvalidChannel", $"Invalid channel: {request.Channel}");

        var result = request.Action.ToLowerInvariant() switch
        {
            "approve" => approval.Approve(UserId.From(request.UserId), channel, request.Comment),
            "reject" => approval.Reject(UserId.From(request.UserId), channel, request.Comment),
            _ => Error.Validation("Approval.InvalidAction", $"Invalid action: {request.Action}")
        };

        if (result.IsFailure) return result;

        await approvalRepository.UpdateAsync(approval, ct);
        await eventDispatcher.DispatchEventsAsync(approval, ct);
        return Result.Success();
    }
}
