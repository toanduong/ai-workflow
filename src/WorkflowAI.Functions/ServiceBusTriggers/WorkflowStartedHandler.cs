using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Executions.Commands.StartExecution;
using WorkflowAI.Infrastructure.Messaging.ServiceBus.IntegrationEvents;

namespace WorkflowAI.Functions.ServiceBusTriggers;

public sealed class WorkflowStartedHandler(IMediator mediator, ILogger<WorkflowStartedHandler> logger)
{
    [Function("HandleWorkflowStarted")]
    public async Task Run(
        [ServiceBusTrigger("workflow-events", "ai-agent-processor", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        FunctionContext context)
    {
        var integrationEvent = JsonSerializer.Deserialize<WorkflowStartedIntegrationEvent>(
            message.Body.ToString(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (integrationEvent is null)
        {
            logger.LogWarning("Failed to deserialize WorkflowStartedIntegrationEvent");
            return;
        }

        logger.LogInformation(
            "Workflow started: ExecutionId={ExecutionId}, WorkflowId={WorkflowId}",
            integrationEvent.ExecutionId, integrationEvent.WorkflowId);

        await mediator.Send(new StartExecutionCommand(integrationEvent.WorkflowId, integrationEvent.ExecutionId));
    }
}
