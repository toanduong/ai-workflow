namespace WorkflowAI.Domain.AIAgent;

public readonly record struct AIAgentTaskId(Guid Value)
{
    public static AIAgentTaskId New() => new(Guid.NewGuid());
    public static AIAgentTaskId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
