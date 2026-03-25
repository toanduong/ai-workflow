using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Notifications;

public sealed class Notification : Entity<NotificationId>
{
    public Guid? ApprovalRequestId { get; private set; }
    public Guid ChannelId { get; private set; }
    public NotificationType Type { get; private set; } = NotificationType.ApprovalRequest;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; } = NotificationStatus.Queued;
    public string RecipientAddress { get; private set; } = string.Empty;
    public int RetryCount { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }

    private Notification() { }

    public static Notification Create(
        Guid? approvalRequestId,
        Guid channelId,
        NotificationType type,
        string subject,
        string body,
        string recipientAddress)
    {
        return new Notification
        {
            Id = NotificationId.New(),
            ApprovalRequestId = approvalRequestId,
            ChannelId = channelId,
            Type = type,
            Subject = subject,
            Body = body,
            RecipientAddress = recipientAddress,
            Status = NotificationStatus.Queued
        };
    }

    public void MarkSent()
    {
        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDelivered()
    {
        Status = NotificationStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        Status = NotificationStatus.Failed;
        RetryCount++;
        UpdatedAt = DateTime.UtcNow;
    }
}
