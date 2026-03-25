namespace WorkflowAI.Infrastructure.Messaging.ServiceBus.IntegrationEvents;

public sealed record StepExecutionRequestedIntegrationEvent(
    Guid ExecutionId,
    Guid StepExecutionId,
    string StepType) : IntegrationEvent;
