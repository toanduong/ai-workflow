namespace WorkflowAI.Application.Common.Interfaces;

public interface INotificationSender
{
    Task<bool> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}

public sealed record NotificationRequest(
    string ChannelType,
    string RecipientAddress,
    string Subject,
    string Body,
    string? ActionUrl = null,
    string? RejectUrl = null);
