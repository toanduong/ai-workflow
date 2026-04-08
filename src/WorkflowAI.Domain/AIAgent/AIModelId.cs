namespace WorkflowAI.Domain.AIAgent;

public readonly record struct AIModelId(Guid Value)
{
    public static AIModelId New() => new(Guid.NewGuid());
    public static AIModelId From(Guid value) => new(value);
}
