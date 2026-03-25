using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Domain.Approvals;

public sealed class ApprovalAction : Entity<Guid>
{
    public ApprovalRequestId ApprovalRequestId { get; private set; }
    public UserId UserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Comment { get; private set; }
    public ApprovalChannel Channel { get; private set; } = ApprovalChannel.Web;
    public DateTime ActedAt { get; private set; }

    private ApprovalAction() { }

    public static ApprovalAction Create(
        ApprovalRequestId approvalRequestId,
        UserId userId,
        string action,
        ApprovalChannel channel,
        string? comment = null)
    {
        return new ApprovalAction
        {
            Id = Guid.NewGuid(),
            ApprovalRequestId = approvalRequestId,
            UserId = userId,
            Action = action,
            Channel = channel,
            Comment = comment,
            ActedAt = DateTime.UtcNow
        };
    }
}
