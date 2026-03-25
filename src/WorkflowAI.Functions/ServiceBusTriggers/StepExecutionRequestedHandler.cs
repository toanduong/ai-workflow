using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.AIAgent.Commands.ExecuteAIStep;
using WorkflowAI.Infrastructure.Messaging.ServiceBus.IntegrationEvents;

namespace WorkflowAI.Functions.ServiceBusTriggers;

public sealed class StepExecutionRequestedHandler(IMediator mediator, ILogger<StepExecutionRequestedHandler> logger)
{
    [Function("HandleStepExecutionRequested")]
    public async Task Run(
        [ServiceBusTrigger("step-events", "step-executor", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        FunctionContext context)
    {
        var integrationEvent = JsonSerializer.Deserialize<StepExecutionRequestedIntegrationEvent>(
            message.Body.ToString(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (integrationEvent is null)
        {
            logger.LogWarning("Failed to deserialize StepExecutionRequestedIntegrationEvent");
            return;
        }

        logger.LogInformation(
            "Step execution requested: StepExecutionId={StepExecutionId}",
            integrationEvent.StepExecutionId);

        await mediator.Send(new ExecuteAIStepCommand(integrationEvent.StepExecutionId, null));
    }
}
