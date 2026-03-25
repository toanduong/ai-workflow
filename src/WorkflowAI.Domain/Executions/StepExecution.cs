using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Executions;

public sealed class StepExecution : Entity<Guid>
{
    public ExecutionId WorkflowExecutionId { get; private set; }
    public Guid WorkflowStepId { get; private set; }
    public StepExecutionStatus Status { get; private set; } = StepExecutionStatus.Pending;
    public string? InputData { get; private set; }
    public string? OutputData { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    private StepExecution() { }

    public static StepExecution Create(ExecutionId executionId, Guid workflowStepId, string? inputData = null)
    {
        return new StepExecution
        {
            Id = Guid.NewGuid(),
            WorkflowExecutionId = executionId,
            WorkflowStepId = workflowStepId,
            InputData = inputData,
            Status = StepExecutionStatus.Pending
        };
    }

    public void Start()
    {
        Status = StepExecutionStatus.InProgress;
        StartedAt = DateTime.UtcNow;
    }

    public void WaitForApproval()
    {
        Status = StepExecutionStatus.WaitingApproval;
    }

    public void Approve()
    {
        Status = StepExecutionStatus.Approved;
    }

    public void Reject()
    {
        Status = StepExecutionStatus.Rejected;
        CompletedAt = DateTime.UtcNow;
    }

    public void Complete(string? outputData)
    {
        Status = StepExecutionStatus.Completed;
        OutputData = outputData;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string errorMessage)
    {
        Status = StepExecutionStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    public void Skip()
    {
        Status = StepExecutionStatus.Skipped;
        CompletedAt = DateTime.UtcNow;
    }
}
