namespace WorkflowAI.Infrastructure.Messaging.ServiceBus.IntegrationEvents;

public sealed record WorkflowStartedIntegrationEvent(
    Guid ExecutionId,
    Guid WorkflowId) : IntegrationEvent;
