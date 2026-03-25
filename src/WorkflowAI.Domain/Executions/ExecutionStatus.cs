using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Executions;

public sealed class ExecutionStatus : Enumeration<ExecutionStatus>
{
    public static readonly ExecutionStatus Running = new(1, nameof(Running));
    public static readonly ExecutionStatus Paused = new(2, nameof(Paused));
    public static readonly ExecutionStatus Completed = new(3, nameof(Completed));
    public static readonly ExecutionStatus Failed = new(4, nameof(Failed));
    public static readonly ExecutionStatus Cancelled = new(5, nameof(Cancelled));

    private ExecutionStatus(int id, string name) : base(id, name) { }
}
