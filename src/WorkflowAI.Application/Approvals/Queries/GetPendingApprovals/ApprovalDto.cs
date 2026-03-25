namespace WorkflowAI.Application.Approvals.Queries.GetPendingApprovals;

public sealed record ApprovalDto(
    Guid Id, Guid StepExecutionId, string Title, string? Description,
    string Status, DateTime ExpiresAt, DateTime CreatedAt);
