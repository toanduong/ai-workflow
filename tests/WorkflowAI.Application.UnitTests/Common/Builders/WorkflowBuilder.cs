using WorkflowAI.Domain.Users;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.UnitTests.Common.Builders;

internal sealed class WorkflowBuilder
{
    private string _name = "Test Workflow";
    private string? _description;
    private UserId _userId = UserId.New();
    private Guid? _templateId;
    private readonly List<(string Name, StepType StepType)> _steps = [];
    private bool _activate;

    public WorkflowBuilder WithName(string name) { _name = name; return this; }
    public WorkflowBuilder WithDescription(string description) { _description = description; return this; }
    public WorkflowBuilder WithUserId(UserId userId) { _userId = userId; return this; }
    public WorkflowBuilder WithTemplateId(Guid templateId) { _templateId = templateId; return this; }
    public WorkflowBuilder WithStep(string name, StepType stepType) { _steps.Add((name, stepType)); return this; }
    public WorkflowBuilder Activated() { _activate = true; return this; }

    public Workflow Build()
    {
        var workflow = Workflow.Create(_name, _description, _userId, _templateId);

        if (_activate && _steps.Count == 0)
            _steps.Add(("Default Step", StepType.Action));

        foreach (var (name, stepType) in _steps)
            workflow.AddStep(name, stepType);

        if (_activate)
            workflow.Activate();

        workflow.ClearDomainEvents();
        return workflow;
    }
}
