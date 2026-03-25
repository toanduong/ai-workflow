using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Workflows.Commands.UpdateWorkflow;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Workflows;

public sealed class UpdateWorkflowFunction(IMediator mediator)
{
    [Function("UpdateWorkflow")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "workflows/{workflowId}")]
        HttpRequestData req,
        Guid workflowId)
    {
        var body = await req.ReadFromJsonAsync<UpdateWorkflowRequest>();
        if (body is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { Message = "Invalid request body." });
            return badRequest;
        }

        var command = new UpdateWorkflowCommand(workflowId, body.Name, body.Description);
        var result = await mediator.Send(command);
        return await req.CreateResultResponseAsync(result);
    }

    private sealed record UpdateWorkflowRequest(string Name, string? Description);
}
