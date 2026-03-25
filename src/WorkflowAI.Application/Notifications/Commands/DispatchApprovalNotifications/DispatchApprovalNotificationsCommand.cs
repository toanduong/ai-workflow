using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Notifications.Commands.DispatchApprovalNotifications;

public sealed record DispatchApprovalNotificationsCommand(
    Guid ApprovalRequestId, List<string> Channels, List<Guid> RecipientUserIds) : IRequest<Result>;
