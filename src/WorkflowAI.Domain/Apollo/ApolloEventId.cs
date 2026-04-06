namespace WorkflowAI.Domain.Apollo;

public readonly record struct ApolloEventId(Guid Value)
{
    public static ApolloEventId New() => new(Guid.NewGuid());
    public static ApolloEventId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
