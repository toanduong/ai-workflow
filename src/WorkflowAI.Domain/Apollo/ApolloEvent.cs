using WorkflowAI.Domain.Apollo.Events;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Apollo;

public sealed class ApolloEvent : AggregateRoot<ApolloEventId>
{
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public string? ClaudeResponse { get; private set; }
    public Guid? WorkflowExecutionId { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public ApolloEventStatus Status { get; private set; } = ApolloEventStatus.Received;
    public string? ErrorMessage { get; private set; }

    private ApolloEvent() { }

    public static ApolloEvent Create(string eventType, string payload)
    {
        var apolloEvent = new ApolloEvent
        {
            Id = ApolloEventId.New(),
            EventType = eventType,
            Payload = payload,
            Status = ApolloEventStatus.Received
        };
        apolloEvent.RaiseDomainEvent(new ApolloEventCreatedEvent(apolloEvent.Id));
        return apolloEvent;
    }

    public void MarkProcessing()
    {
        Status = ApolloEventStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(string claudeResponse, Guid? executionId = null)
    {
        Status = ApolloEventStatus.Processed;
        ClaudeResponse = claudeResponse;
        WorkflowExecutionId = executionId;
        ProcessedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new ApolloEventProcessedEvent(Id, executionId));
    }

    public void Fail(string errorMessage)
    {
        Status = ApolloEventStatus.Failed;
        ErrorMessage = errorMessage;
        UpdatedAt = DateTime.UtcNow;
    }
}
