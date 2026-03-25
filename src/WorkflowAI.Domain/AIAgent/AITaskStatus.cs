using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.AIAgent;

public sealed class AITaskStatus : Enumeration<AITaskStatus>
{
    public static readonly AITaskStatus Pending = new(1, nameof(Pending));
    public static readonly AITaskStatus Processing = new(2, nameof(Processing));
    public static readonly AITaskStatus Completed = new(3, nameof(Completed));
    public static readonly AITaskStatus Failed = new(4, nameof(Failed));

    private AITaskStatus(int id, string name) : base(id, name) { }
}
