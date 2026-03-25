using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Notifications.Commands.SendNotification;

public sealed class SendNotificationCommandHandler(INotificationSender notificationSender)
    : IRequestHandler<SendNotificationCommand, Result>
{
    public async Task<Result> Handle(SendNotificationCommand request, CancellationToken ct)
    {
        var notifRequest = new NotificationRequest(
            request.ChannelType, request.RecipientAddress, request.Subject, request.Body);
        await notificationSender.SendAsync(notifRequest, ct);
        return Result.Success();
    }
}
