using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Channels;

public sealed class ChannelType : Enumeration<ChannelType>
{
    public static readonly ChannelType Email = new(1, nameof(Email));
    public static readonly ChannelType Slack = new(2, nameof(Slack));
    public static readonly ChannelType Teams = new(3, nameof(Teams));

    private ChannelType(int id, string name) : base(id, name) { }
}
