namespace WorkflowAI.Domain.Executions;

public readonly record struct ExecutionId(Guid Value)
{
    public static ExecutionId New() => new(Guid.NewGuid());
    public static ExecutionId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
