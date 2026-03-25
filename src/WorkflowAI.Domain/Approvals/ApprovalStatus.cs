using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Approvals;

public sealed class ApprovalStatus : Enumeration<ApprovalStatus>
{
    public static readonly ApprovalStatus Pending = new(1, nameof(Pending));
    public static readonly ApprovalStatus Approved = new(2, nameof(Approved));
    public static readonly ApprovalStatus Rejected = new(3, nameof(Rejected));
    public static readonly ApprovalStatus Escalated = new(4, nameof(Escalated));
    public static readonly ApprovalStatus TimedOut = new(5, nameof(TimedOut));

    private ApprovalStatus(int id, string name) : base(id, name) { }
}
