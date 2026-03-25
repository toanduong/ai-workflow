using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Domain.Executions.Events;

public sealed record ExecutionCompletedEvent(ExecutionId ExecutionId, WorkflowId WorkflowId) : IDomainEvent;
