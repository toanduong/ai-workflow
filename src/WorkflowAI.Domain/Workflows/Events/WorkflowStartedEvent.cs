using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Executions;

namespace WorkflowAI.Domain.Workflows.Events;

public sealed record WorkflowStartedEvent(WorkflowId WorkflowId, ExecutionId ExecutionId) : IDomainEvent;
