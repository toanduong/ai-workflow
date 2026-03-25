using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Notifications.Commands.DispatchApprovalNotifications;
using WorkflowAI.Infrastructure.Messaging.ServiceBus.IntegrationEvents;

namespace WorkflowAI.Functions.ServiceBusTriggers;

public sealed class ApprovalRequestedHandler(IMediator mediator, ILogger<ApprovalRequestedHandler> logger)
{
    [Function("HandleApprovalRequested")]
    public async Task Run(
        [ServiceBusTrigger("approval-events", "notification-dispatcher", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        FunctionContext context)
    {
        var integrationEvent = JsonSerializer.Deserialize<ApprovalRequestedIntegrationEvent>(
            message.Body.ToString(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (integrationEvent is null)
        {
            logger.LogWarning("Failed to deserialize ApprovalRequestedIntegrationEvent");
            return;
        }

        logger.LogInformation(
            "Approval requested: ApprovalRequestId={ApprovalRequestId}",
            integrationEvent.ApprovalRequestId);

        await mediator.Send(new DispatchApprovalNotificationsCommand(
            integrationEvent.ApprovalRequestId,
            integrationEvent.Channels,
            integrationEvent.RecipientUserIds));
    }
}
