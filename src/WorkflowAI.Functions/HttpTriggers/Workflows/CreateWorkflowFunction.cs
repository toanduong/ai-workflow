using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Workflows.Commands.CreateWorkflow;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Workflows;

public sealed class CreateWorkflowFunction(IMediator mediator)
{
    [Function("CreateWorkflow")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "workflows")]
        HttpRequestData req)
    {
        var command = await req.ReadFromJsonAsync<CreateWorkflowCommand>();
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
