using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Workflows.Commands.ActivateWorkflow;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Workflows;

public sealed class ActivateWorkflowFunction(IMediator mediator)
{
    [Function("ActivateWorkflow")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "workflows/{workflowId}/activate")]
        HttpRequestData req,
        Guid workflowId)
    {
        var command = new ActivateWorkflowCommand(workflowId);
        var result = await mediator.Send(command);
        return await req.CreateResultResponseAsync(result);
    }
}
