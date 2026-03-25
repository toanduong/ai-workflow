using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Workflows;

public sealed class WorkflowStatus : Enumeration<WorkflowStatus>
{
    public static readonly WorkflowStatus Draft = new(1, nameof(Draft));
    public static readonly WorkflowStatus Active = new(2, nameof(Active));
    public static readonly WorkflowStatus Archived = new(3, nameof(Archived));

    private WorkflowStatus(int id, string name) : base(id, name) { }
}
