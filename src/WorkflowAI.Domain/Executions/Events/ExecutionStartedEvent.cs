using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Domain.Executions.Events;

public sealed record ExecutionStartedEvent(ExecutionId ExecutionId, WorkflowId WorkflowId) : IDomainEvent;
