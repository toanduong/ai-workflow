using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Connectors.Commands.RefreshConnectorToken;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Functions.TimerTriggers;

public sealed class ConnectorTokenRefreshFunction(
    IConnectorRepository connectorRepository,
    IMediator mediator,
    ILogger<ConnectorTokenRefreshFunction> logger)
{
    [Function("RefreshConnectorTokens")]
    public async Task Run(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timer,
        FunctionContext context)
    {
        logger.LogInformation("Checking for expiring connector tokens...");

        var expiring = await connectorRepository.GetExpiringAsync(TimeSpan.FromMinutes(15));
        foreach (var connector in expiring)
        {
            logger.LogInformation("Refreshing token for connector {ConnectorId}", connector.Id.Value);
            await mediator.Send(new RefreshConnectorTokenCommand(connector.Id.Value));
        }

        logger.LogInformation("Processed {Count} connector token refreshes.", expiring.Count);
    }
}
