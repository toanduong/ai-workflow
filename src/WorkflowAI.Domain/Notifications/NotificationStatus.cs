using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Notifications;

public sealed class NotificationStatus : Enumeration<NotificationStatus>
{
    public static readonly NotificationStatus Queued = new(1, nameof(Queued));
    public static readonly NotificationStatus Sent = new(2, nameof(Sent));
    public static readonly NotificationStatus Delivered = new(3, nameof(Delivered));
    public static readonly NotificationStatus Failed = new(4, nameof(Failed));

    private NotificationStatus(int id, string name) : base(id, name) { }
}
