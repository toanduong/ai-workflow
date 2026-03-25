using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Connectors.Commands.DeleteConnector;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Connectors;

public sealed class DeleteConnectorFunction(IMediator mediator)
{
    [Function("DeleteConnector")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "connectors/{connectorId}")]
        HttpRequestData req,
        string connectorId)
    {
        var result = await mediator.Send(new DeleteConnectorCommand(Guid.Parse(connectorId)));
        return await req.CreateResultResponseAsync(result);
    }
}
