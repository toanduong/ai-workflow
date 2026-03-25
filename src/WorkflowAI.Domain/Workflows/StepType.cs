using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Workflows;

public sealed class StepType : Enumeration<StepType>
{
    public static readonly StepType AIAgent = new(1, nameof(AIAgent));
    public static readonly StepType HumanApproval = new(2, nameof(HumanApproval));
    public static readonly StepType Notification = new(3, nameof(Notification));
    public static readonly StepType Action = new(4, nameof(Action));

    private StepType(int id, string name) : base(id, name) { }
}
