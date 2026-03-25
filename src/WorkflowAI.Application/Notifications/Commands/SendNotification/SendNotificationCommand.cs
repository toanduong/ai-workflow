using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Notifications.Commands.SendNotification;

public sealed record SendNotificationCommand(
    Guid NotificationId, string ChannelType, string RecipientAddress,
    string Subject, string Body) : IRequest<Result>;
