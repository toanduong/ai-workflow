using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Domain.Workflows;

public sealed class WorkflowStep : Entity<Guid>
{
    public WorkflowId WorkflowId { get; private set; }
    public int OrderIndex { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public StepType StepType { get; private set; } = StepType.Action;
    public string? HttpMethod { get; private set; }
    public string? Configuration { get; private set; }
    public string? RequiredRole { get; private set; }
    public int TimeoutMinutes { get; private set; }
    public TimeoutAction OnTimeoutAction { get; private set; } = TimeoutAction.Escalate;
    public ConnectorId? ConnectorId { get; private set; }

    private WorkflowStep() { }

    public static WorkflowStep Create(
        WorkflowId workflowId,
        int orderIndex,
        string name,
        StepType stepType,
        string? httpMethod = null,
        string? configuration = null,
        string? requiredRole = null,
        int timeoutMinutes = 60,
        TimeoutAction? onTimeoutAction = null,
        ConnectorId? connectorId = null)
    {
        return new WorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            OrderIndex = orderIndex,
            Name = name,
            StepType = stepType,
            HttpMethod = httpMethod,
            Configuration = configuration,
            RequiredRole = requiredRole,
            TimeoutMinutes = timeoutMinutes,
            OnTimeoutAction = onTimeoutAction ?? TimeoutAction.Escalate,
            ConnectorId = connectorId
        };
    }

    public void UpdateConfiguration(string? configuration)
    {
        Configuration = configuration;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateOrder(int orderIndex)
    {
        OrderIndex = orderIndex;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetConnector(ConnectorId? connectorId)
    {
        ConnectorId = connectorId;
        UpdatedAt = DateTime.UtcNow;
    }

    public static WorkflowStep Restore(
        Guid id,
        WorkflowId workflowId,
        int orderIndex,
        string name,
        StepType stepType,
        string? httpMethod,
        string? configuration,
        string? requiredRole,
        int timeoutMinutes,
        TimeoutAction onTimeoutAction,
        ConnectorId? connectorId,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        return new WorkflowStep
        {
            Id = id,
            WorkflowId = workflowId,
            OrderIndex = orderIndex,
            Name = name,
            StepType = stepType,
            HttpMethod = httpMethod,
            Configuration = configuration,
            RequiredRole = requiredRole,
            TimeoutMinutes = timeoutMinutes,
            OnTimeoutAction = onTimeoutAction,
            ConnectorId = connectorId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
