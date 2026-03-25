using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Connectors.Commands.CompleteOAuthFlow;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Connectors;

public sealed class OAuthCallbackFunction(IMediator mediator)
{
    [Function("OAuthCallback")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "connectors/oauth/callback")]
        HttpRequestData req)
    {
        var command = await req.ReadFromJsonAsync<CompleteOAuthFlowCommand>();
        if (command is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { Message = "Invalid request body." });
            return badRequest;
        }

        var result = await mediator.Send(command);
        return await req.CreateResultResponseAsync(result);
    }
}
