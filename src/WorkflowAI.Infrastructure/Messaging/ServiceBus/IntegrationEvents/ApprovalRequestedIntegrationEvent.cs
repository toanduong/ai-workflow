namespace WorkflowAI.Infrastructure.Messaging.ServiceBus.IntegrationEvents;

public sealed record ApprovalRequestedIntegrationEvent(
    Guid ApprovalRequestId,
    Guid StepExecutionId,
    List<string> Channels,
    List<Guid> RecipientUserIds) : IntegrationEvent;
