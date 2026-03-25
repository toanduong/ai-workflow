using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Notifications.Commands.SendNotification;
using WorkflowAI.Infrastructure.Messaging.ServiceBus.IntegrationEvents;

namespace WorkflowAI.Functions.ServiceBusTriggers;

public sealed class NotificationRequestedHandler(IMediator mediator, ILogger<NotificationRequestedHandler> logger)
{
    [Function("HandleNotificationRequested")]
    public async Task Run(
        [ServiceBusTrigger("notification-events", "channel-dispatcher", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        FunctionContext context)
    {
        var integrationEvent = JsonSerializer.Deserialize<NotificationRequestedIntegrationEvent>(
            message.Body.ToString(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (integrationEvent is null)
        {
            logger.LogWarning("Failed to deserialize NotificationRequestedIntegrationEvent");
            return;
        }

        logger.LogInformation(
            "Notification requested: Channel={Channel}, Recipient={Recipient}",
            integrationEvent.ChannelType, integrationEvent.RecipientAddress);

        await mediator.Send(new SendNotificationCommand(
            integrationEvent.NotificationId,
            integrationEvent.ChannelType,
            integrationEvent.RecipientAddress,
            integrationEvent.Subject,
            integrationEvent.Body));
    }
}
