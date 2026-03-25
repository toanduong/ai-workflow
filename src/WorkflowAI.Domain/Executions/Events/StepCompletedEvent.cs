using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Executions.Events;

public sealed record StepCompletedEvent(ExecutionId ExecutionId, Guid StepExecutionId) : IDomainEvent;
