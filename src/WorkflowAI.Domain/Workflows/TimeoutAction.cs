using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Workflows;

public sealed class TimeoutAction : Enumeration<TimeoutAction>
{
    public static readonly TimeoutAction Escalate = new(1, nameof(Escalate));
    public static readonly TimeoutAction AutoApprove = new(2, nameof(AutoApprove));
    public static readonly TimeoutAction AutoReject = new(3, nameof(AutoReject));

    private TimeoutAction(int id, string name) : base(id, name) { }
}
