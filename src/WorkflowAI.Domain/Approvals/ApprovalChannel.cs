using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Approvals;

public sealed class ApprovalChannel : Enumeration<ApprovalChannel>
{
    public static readonly ApprovalChannel Web = new(1, nameof(Web));
    public static readonly ApprovalChannel Email = new(2, nameof(Email));
    public static readonly ApprovalChannel Slack = new(3, nameof(Slack));
    public static readonly ApprovalChannel Teams = new(4, nameof(Teams));

    private ApprovalChannel(int id, string name) : base(id, name) { }
}
