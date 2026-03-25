using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Connectors.Queries.ListConnectors;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Connectors;

public sealed class ListConnectorsFunction(IMediator mediator)
{
    [Function("ListConnectors")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "connectors")]
        HttpRequestData req)
    {
        var result = await mediator.Send(new ListConnectorsQuery());
        return await req.CreateResultResponseAsync(result);
    }
}
