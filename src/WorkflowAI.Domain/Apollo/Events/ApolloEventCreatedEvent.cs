using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Apollo.Events;

public sealed record ApolloEventCreatedEvent(ApolloEventId ApolloEventId) : IDomainEvent;
