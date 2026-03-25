namespace WorkflowAI.Infrastructure.Messaging.ServiceBus.IntegrationEvents;

public sealed record ApprovalCompletedIntegrationEvent(
    Guid ApprovalRequestId,
    Guid StepExecutionId,
    string Action) : IntegrationEvent;
