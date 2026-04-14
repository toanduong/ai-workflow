using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Connectors;

public sealed class ConnectorType : Enumeration<ConnectorType>
{
    public static readonly ConnectorType Office365 = new(1, nameof(Office365));
    public static readonly ConnectorType ACS = new(2, nameof(ACS));
    public static readonly ConnectorType Slack = new(3, nameof(Slack));
    public static readonly ConnectorType Teams = new(4, nameof(Teams));
    public static readonly ConnectorType SendGrid = new(5, nameof(SendGrid));
    public static readonly ConnectorType Custom = new(6, nameof(Custom));
    public static readonly ConnectorType Apollo = new(7, nameof(Apollo));
    public static readonly ConnectorType Odoo = new(8, nameof(Odoo));

    private ConnectorType(int id, string name) : base(id, name) { }
}
