using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Notifications;

public sealed class NotificationType : Enumeration<NotificationType>
{
    public static readonly NotificationType ApprovalRequest = new(1, nameof(ApprovalRequest));
    public static readonly NotificationType ApprovalResult = new(2, nameof(ApprovalResult));
    public static readonly NotificationType WorkflowComplete = new(3, nameof(WorkflowComplete));
    public static readonly NotificationType Error = new(4, nameof(Error));

    private NotificationType(int id, string name) : base(id, name) { }
}
