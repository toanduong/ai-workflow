using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Connectors.Commands.ValidateConnector;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Connectors;

public sealed class ValidateConnectorFunction(IMediator mediator)
{
    [Function("ValidateConnector")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "connectors/{connectorId}/validate")]
        HttpRequestData req,
        string connectorId)
    {
        var result = await mediator.Send(new ValidateConnectorCommand(Guid.Parse(connectorId)));
        return await req.CreateResultResponseAsync(result);
    }
}
