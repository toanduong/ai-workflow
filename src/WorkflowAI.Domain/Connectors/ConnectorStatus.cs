using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Connectors;

public sealed class ConnectorStatus : Enumeration<ConnectorStatus>
{
    public static readonly ConnectorStatus Created = new(1, nameof(Created));
    public static readonly ConnectorStatus Validating = new(2, nameof(Validating));
    public static readonly ConnectorStatus Active = new(3, nameof(Active));
    public static readonly ConnectorStatus Failed = new(4, nameof(Failed));
    public static readonly ConnectorStatus Expired = new(5, nameof(Expired));

    private ConnectorStatus(int id, string name) : base(id, name) { }
}
