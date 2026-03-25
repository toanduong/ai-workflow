using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Executions.Commands.AdvanceStep;
using WorkflowAI.Infrastructure.Messaging.ServiceBus.IntegrationEvents;

namespace WorkflowAI.Functions.ServiceBusTriggers;

public sealed class ApprovalCompletedHandler(IMediator mediator, ILogger<ApprovalCompletedHandler> logger)
{
    [Function("HandleApprovalCompleted")]
    public async Task Run(
        [ServiceBusTrigger("approval-events", "workflow-advancer", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        FunctionContext context)
    {
        var integrationEvent = JsonSerializer.Deserialize<ApprovalCompletedIntegrationEvent>(
            message.Body.ToString(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (integrationEvent is null)
        {
            logger.LogWarning("Failed to deserialize ApprovalCompletedIntegrationEvent");
            return;
        }

        logger.LogInformation(
            "Approval completed: ApprovalRequestId={ApprovalRequestId}, Action={Action}",
            integrationEvent.ApprovalRequestId, integrationEvent.Action);

        await mediator.Send(new AdvanceStepCommand(integrationEvent.StepExecutionId, integrationEvent.StepExecutionId));
    }
}
