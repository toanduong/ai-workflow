using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Apollo.Events;

public sealed record ApolloEventProcessedEvent(ApolloEventId ApolloEventId, Guid? ExecutionId) : IDomainEvent;
