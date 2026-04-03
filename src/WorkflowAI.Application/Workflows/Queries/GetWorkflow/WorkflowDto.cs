namespace WorkflowAI.Application.Workflows.Queries.GetWorkflow;

public sealed record WorkflowDto(
    Guid Id,
    string Name,
    string? Description,
    string Status,
    IReadOnlyList<WorkflowStepDto> Steps,
    DateTime CreatedAt,
    string? LogicAppResourceId);

public sealed record WorkflowStepDto(
    Guid Id,
    int OrderIndex,
    string Name,
    string StepType,
    string? Method,
    string? Configuration,
    string? RequiredRole,
    int TimeoutMinutes,
    string OnTimeoutAction,
    Guid? ConnectorId);
