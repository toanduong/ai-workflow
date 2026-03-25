using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Workflows.Events;

public sealed record WorkflowCreatedEvent(WorkflowId WorkflowId) : IDomainEvent;
