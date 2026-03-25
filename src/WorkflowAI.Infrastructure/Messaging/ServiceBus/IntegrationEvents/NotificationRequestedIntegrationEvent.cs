namespace WorkflowAI.Infrastructure.Messaging.ServiceBus.IntegrationEvents;

public sealed record NotificationRequestedIntegrationEvent(
    Guid NotificationId,
    string ChannelType,
    string RecipientAddress,
    string Subject,
    string Body) : IntegrationEvent;
