using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Approvals.Events;

public sealed record ApprovalCompletedEvent(
    ApprovalRequestId ApprovalRequestId,
    Guid StepExecutionId,
    string Action) : IDomainEvent;
