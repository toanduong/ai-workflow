using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.UnitTests.Common.Builders;

internal sealed class WorkflowExecutionBuilder
{
    private WorkflowId _workflowId = WorkflowId.New();
    private string _triggeredBy = "Test User";
    private string? _inputData;
    private readonly List<Guid> _steps = [];

    public WorkflowExecutionBuilder WithWorkflowId(WorkflowId id) { _workflowId = id; return this; }
    public WorkflowExecutionBuilder WithTriggeredBy(string triggeredBy) { _triggeredBy = triggeredBy; return this; }
    public WorkflowExecutionBuilder WithInputData(string inputData) { _inputData = inputData; return this; }
    public WorkflowExecutionBuilder WithStep(Guid workflowStepId) { _steps.Add(workflowStepId); return this; }

    public WorkflowExecution Build()
    {
        var execution = WorkflowExecution.Create(_workflowId, _triggeredBy, _inputData);
        foreach (var stepId in _steps)
            execution.AddStep(stepId);
        execution.ClearDomainEvents();
        return execution;
    }
}
