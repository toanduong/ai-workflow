using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Approvals.Events;

public sealed record ApprovalRequestedEvent(ApprovalRequestId ApprovalRequestId, Guid StepExecutionId) : IDomainEvent;
