namespace WorkflowAI.Domain.Connectors;

public readonly record struct ConnectorId(Guid Value)
{
    public static ConnectorId New() => new(Guid.NewGuid());
    public static ConnectorId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
