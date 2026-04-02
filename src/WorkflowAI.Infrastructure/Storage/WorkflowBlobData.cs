using WorkflowAI.Domain.Connectors;
using WorkflowAI.Domain.Users;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Infrastructure.Storage;

internal sealed class WorkflowBlobData
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = "Draft";
    public Guid CreatedByUserId { get; init; }
    public Guid? TemplateId { get; init; }
    public string? LogicAppResourceId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public List<WorkflowStepBlobData> Steps { get; init; } = [];

    public static WorkflowBlobData FromDomain(Workflow workflow)
    {
        return new WorkflowBlobData
        {
            Id = workflow.Id.Value,
            Name = workflow.Name,
            Description = workflow.Description,
            Status = workflow.Status.Name,
            CreatedByUserId = workflow.CreatedByUserId.Value,
            TemplateId = workflow.TemplateId,
            LogicAppResourceId = workflow.LogicAppResourceId,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt,
            Steps = workflow.Steps.Select(WorkflowStepBlobData.FromDomain).ToList()
        };
    }

    public Workflow ToDomain()
    {
        var steps = Steps.Select(s => s.ToDomain());
        return Workflow.Restore(
            WorkflowId.From(Id),
            Name,
            Description,
            WorkflowStatus.FromName(Status) ?? WorkflowStatus.Draft,
            UserId.From(CreatedByUserId),
            TemplateId,
            LogicAppResourceId,
            CreatedAt,
            UpdatedAt,
            steps);
    }
}

internal sealed class WorkflowStepBlobData
{
    public Guid Id { get; init; }
    public Guid WorkflowId { get; init; }
    public int OrderIndex { get; init; }
    public string Name { get; init; } = string.Empty;
    public string StepTypeName { get; init; } = "Action";
    public string? Configuration { get; init; }
    public string? RequiredRole { get; init; }
    public int TimeoutMinutes { get; init; }
    public string OnTimeoutActionName { get; init; } = "Escalate";
    public Guid? ConnectorIdValue { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public static WorkflowStepBlobData FromDomain(WorkflowStep step)
    {
        return new WorkflowStepBlobData
        {
            Id = step.Id,
            WorkflowId = step.WorkflowId.Value,
            OrderIndex = step.OrderIndex,
            Name = step.Name,
            StepTypeName = step.StepType.Name,
            Configuration = step.Configuration,
            RequiredRole = step.RequiredRole,
            TimeoutMinutes = step.TimeoutMinutes,
            OnTimeoutActionName = step.OnTimeoutAction.Name,
            ConnectorIdValue = step.ConnectorId?.Value,
            CreatedAt = step.CreatedAt,
            UpdatedAt = step.UpdatedAt
        };
    }

    public WorkflowStep ToDomain()
    {
        return WorkflowStep.Restore(
            Id,
            Domain.Workflows.WorkflowId.From(WorkflowId),
            OrderIndex,
            Name,
            StepType.FromName(StepTypeName) ?? StepType.Action,
            Configuration,
            RequiredRole,
            TimeoutMinutes,
            TimeoutAction.FromName(OnTimeoutActionName) ?? TimeoutAction.Escalate,
            ConnectorIdValue.HasValue ? ConnectorId.From(ConnectorIdValue.Value) : null,
            CreatedAt,
            UpdatedAt);
    }
}
