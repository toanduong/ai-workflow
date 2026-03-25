using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Connectors.Queries.GetConnector;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Connectors;

public sealed class GetConnectorFunction(IMediator mediator)
{
    [Function("GetConnector")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "connectors/{connectorId}")]
        HttpRequestData req,
        string connectorId)
    {
        var result = await mediator.Send(new GetConnectorQuery(Guid.Parse(connectorId)));
        return await req.CreateResultResponseAsync(result);
    }
}
