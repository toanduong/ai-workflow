using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Executions;

namespace WorkflowAI.Domain.Workflows.Events;

public sealed record WorkflowCompletedEvent(WorkflowId WorkflowId, ExecutionId ExecutionId) : IDomainEvent;
