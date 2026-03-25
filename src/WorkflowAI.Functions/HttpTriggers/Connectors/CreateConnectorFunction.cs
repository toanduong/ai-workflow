using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Connectors.Commands.CreateConnector;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Connectors;

public sealed class CreateConnectorFunction(IMediator mediator)
{
    [Function("CreateConnector")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "connectors")]
        HttpRequestData req)
    {
        var command = await req.ReadFromJsonAsync<CreateConnectorCommand>();
        if (command is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { Message = "Invalid request body." });
            return badRequest;
        }

        var result = await mediator.Send(command);
        return await req.CreateResultResponseAsync(result, HttpStatusCode.Created);
    }
}
