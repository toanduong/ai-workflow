using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.TenantConnectors;

public sealed class TenantConnectorStatus : Enumeration<TenantConnectorStatus>
{
    public static readonly TenantConnectorStatus Pending   = new(1, nameof(Pending));
    public static readonly TenantConnectorStatus Active    = new(2, nameof(Active));
    public static readonly TenantConnectorStatus Failed    = new(3, nameof(Failed));
    public static readonly TenantConnectorStatus Suspended = new(4, nameof(Suspended));

    private TenantConnectorStatus(int id, string name) : base(id, name) { }
}
