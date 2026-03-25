namespace WorkflowAI.Domain.Approvals;

public readonly record struct ApprovalRequestId(Guid Value)
{
    public static ApprovalRequestId New() => new(Guid.NewGuid());
    public static ApprovalRequestId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
