namespace WorkflowAI.Domain.TenantConnectors;

public readonly record struct TenantConnectorApiId(Guid Value)
{
    public static TenantConnectorApiId New() => new(Guid.NewGuid());
    public static TenantConnectorApiId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
