namespace WorkflowAI.Domain.TenantConnectors;

public readonly record struct TenantConnectorId(Guid Value)
{
    public static TenantConnectorId New() => new(Guid.NewGuid());
    public static TenantConnectorId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
