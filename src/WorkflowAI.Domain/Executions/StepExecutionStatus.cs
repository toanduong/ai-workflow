using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Executions;

public sealed class StepExecutionStatus : Enumeration<StepExecutionStatus>
{
    public static readonly StepExecutionStatus Pending = new(1, nameof(Pending));
    public static readonly StepExecutionStatus InProgress = new(2, nameof(InProgress));
    public static readonly StepExecutionStatus WaitingApproval = new(3, nameof(WaitingApproval));
    public static readonly StepExecutionStatus Approved = new(4, nameof(Approved));
    public static readonly StepExecutionStatus Rejected = new(5, nameof(Rejected));
    public static readonly StepExecutionStatus Completed = new(6, nameof(Completed));
    public static readonly StepExecutionStatus Failed = new(7, nameof(Failed));
    public static readonly StepExecutionStatus Skipped = new(8, nameof(Skipped));

    private StepExecutionStatus(int id, string name) : base(id, name) { }
}
