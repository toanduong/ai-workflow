using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Approvals.Events;

public sealed record ApprovalEscalatedEvent(ApprovalRequestId ApprovalRequestId, Guid StepExecutionId) : IDomainEvent;
