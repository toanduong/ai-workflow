namespace WorkflowAI.Application.Executions.Queries.GetExecution;

public sealed record ExecutionDto(
    Guid Id, Guid WorkflowId, string Status, string? InputData, string? OutputData,
    DateTime StartedAt, DateTime? CompletedAt, string TriggeredBy,
    IReadOnlyList<StepExecutionDto> Steps);

public sealed record StepExecutionDto(
    Guid Id, Guid WorkflowStepId, string Status, string? InputData, string? OutputData,
    DateTime? StartedAt, DateTime? CompletedAt, string? ErrorMessage);
