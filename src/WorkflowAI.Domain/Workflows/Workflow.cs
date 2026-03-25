using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Users;
using WorkflowAI.Domain.Workflows.Events;

namespace WorkflowAI.Domain.Workflows;

public sealed class Workflow : AggregateRoot<WorkflowId>
{
    private readonly List<WorkflowStep> _steps = [];

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public WorkflowStatus Status { get; private set; } = WorkflowStatus.Draft;
    public UserId CreatedByUserId { get; private set; }
    public Guid? TemplateId { get; private set; }
    public string? LogicAppResourceId { get; private set; }
    public IReadOnlyList<WorkflowStep> Steps => _steps.AsReadOnly();

    private Workflow() { }

    public static Workflow Create(string name, string? description, UserId createdByUserId, Guid? templateId = null)
    {
        var workflow = new Workflow
        {
            Id = WorkflowId.New(),
            Name = name,
            Description = description,
            CreatedByUserId = createdByUserId,
            TemplateId = templateId,
            Status = WorkflowStatus.Draft
        };

        workflow.RaiseDomainEvent(new WorkflowCreatedEvent(workflow.Id));
        return workflow;
    }

    public Result AddStep(string name, StepType stepType, string? configuration = null,
        string? requiredRole = null, int timeoutMinutes = 60, TimeoutAction? onTimeoutAction = null)
    {
        if (Status != WorkflowStatus.Draft)
            return Error.Validation("Workflow.NotDraft", "Steps can only be added to draft workflows.");

        var step = WorkflowStep.Create(Id, _steps.Count, name, stepType,
            configuration, requiredRole, timeoutMinutes, onTimeoutAction);
        _steps.Add(step);
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }

    public Result Activate()
    {
        if (Status != WorkflowStatus.Draft)
            return Error.Validation("Workflow.NotDraft", "Only draft workflows can be activated.");

        if (_steps.Count == 0)
            return Error.Validation("Workflow.NoSteps", "Workflow must have at least one step.");

        Status = WorkflowStatus.Active;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Archive()
    {
        if (Status == WorkflowStatus.Archived)
            return Error.Validation("Workflow.AlreadyArchived", "Workflow is already archived.");

        Status = WorkflowStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetLogicAppResourceId(string resourceId)
    {
        LogicAppResourceId = resourceId;
        UpdatedAt = DateTime.UtcNow;
    }
}
