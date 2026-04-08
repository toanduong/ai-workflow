namespace WorkflowAI.Domain.TenantConnectors;

public readonly record struct TenantConnectorApiHealthCheckId(Guid Value)
{
    public static TenantConnectorApiHealthCheckId New() => new(Guid.NewGuid());
    public static TenantConnectorApiHealthCheckId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
