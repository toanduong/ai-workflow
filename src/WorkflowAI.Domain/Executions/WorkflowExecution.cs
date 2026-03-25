using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Executions.Events;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Domain.Executions;

public sealed class WorkflowExecution : AggregateRoot<ExecutionId>
{
    private readonly List<StepExecution> _steps = [];

    public WorkflowId WorkflowId { get; private set; }
    public ExecutionStatus Status { get; private set; } = ExecutionStatus.Running;
    public string? InputData { get; private set; }
    public string? OutputData { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string TriggeredBy { get; private set; } = string.Empty;
    public IReadOnlyList<StepExecution> Steps => _steps.AsReadOnly();

    private WorkflowExecution() { }

    public static WorkflowExecution Create(WorkflowId workflowId, string triggeredBy, string? inputData = null)
    {
        var execution = new WorkflowExecution
        {
            Id = ExecutionId.New(),
            WorkflowId = workflowId,
            TriggeredBy = triggeredBy,
            InputData = inputData,
            Status = ExecutionStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        execution.RaiseDomainEvent(new ExecutionStartedEvent(execution.Id, workflowId));
        return execution;
    }

    public StepExecution AddStep(Guid workflowStepId, string? inputData = null)
    {
        var step = StepExecution.Create(Id, workflowStepId, inputData);
        _steps.Add(step);
        return step;
    }

    public Result Complete(string? outputData = null)
    {
        if (Status != ExecutionStatus.Running)
            return Error.Validation("Execution.NotRunning", "Only running executions can be completed.");

        Status = ExecutionStatus.Completed;
        OutputData = outputData;
        CompletedAt = DateTime.UtcNow;

        RaiseDomainEvent(new ExecutionCompletedEvent(Id, WorkflowId));
        return Result.Success();
    }

    public Result Fail(string? reason = null)
    {
        if (Status != ExecutionStatus.Running)
            return Error.Validation("Execution.NotRunning", "Only running executions can be marked as failed.");

        Status = ExecutionStatus.Failed;
        OutputData = reason;
        CompletedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status != ExecutionStatus.Running && Status != ExecutionStatus.Paused)
            return Error.Validation("Execution.CannotCancel", "Only running or paused executions can be cancelled.");

        Status = ExecutionStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        return Result.Success();
    }
}
