using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Apollo;

public sealed class ApolloEventStatus : Enumeration<ApolloEventStatus>
{
    public static readonly ApolloEventStatus Received   = new(1, nameof(Received));
    public static readonly ApolloEventStatus Processing = new(2, nameof(Processing));
    public static readonly ApolloEventStatus Processed  = new(3, nameof(Processed));
    public static readonly ApolloEventStatus Failed     = new(4, nameof(Failed));

    private ApolloEventStatus(int id, string name) : base(id, name) { }
}
