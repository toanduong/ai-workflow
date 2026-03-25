using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Approvals;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Application.Notifications.Commands.DispatchApprovalNotifications;

public sealed class DispatchApprovalNotificationsCommandHandler(
    IApprovalRepository approvalRepository,
    IUserRepository userRepository,
    INotificationSender notificationSender)
    : IRequestHandler<DispatchApprovalNotificationsCommand, Result>
{
    public async Task<Result> Handle(DispatchApprovalNotificationsCommand request, CancellationToken ct)
    {
        var approval = await approvalRepository.GetByIdAsync(ApprovalRequestId.From(request.ApprovalRequestId), ct);
        if (approval is null)
            return Error.NotFound("Approval.NotFound", "Approval request not found.");

        foreach (var userId in request.RecipientUserIds)
        {
            var user = await userRepository.GetByIdAsync(UserId.From(userId), ct);
            if (user is null) continue;

            foreach (var channel in request.Channels)
            {
                var notifRequest = new NotificationRequest(
                    channel, user.Email, approval.Title, approval.Description ?? "");
                await notificationSender.SendAsync(notifRequest, ct);
            }
        }

        return Result.Success();
    }
}
