using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Domain.Channels;

public sealed class NotificationChannel : Entity<ChannelId>
{
    public string Name { get; private set; } = string.Empty;
    public ChannelType ChannelType { get; private set; } = ChannelType.Email;
    public string? ConnectionConfig { get; private set; }
    public bool IsActive { get; private set; } = true;
    public ConnectorId? ConnectorId { get; private set; }

    private NotificationChannel() { }

    public static NotificationChannel Create(string name, ChannelType channelType, string? connectionConfig, ConnectorId? connectorId = null)
    {
        return new NotificationChannel
        {
            Id = ChannelId.New(),
            Name = name,
            ChannelType = channelType,
            ConnectionConfig = connectionConfig,
            ConnectorId = connectorId
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
