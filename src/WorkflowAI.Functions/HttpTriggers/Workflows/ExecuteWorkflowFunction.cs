using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Workflows.Commands.ExecuteWorkflow;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Workflows;

public sealed class ExecuteWorkflowFunction(IMediator mediator)
{
    [Function("ExecuteWorkflow")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "workflows/{workflowId}/execute")]
        HttpRequestData req,
        Guid workflowId)
    {
        var body = await req.ReadFromJsonAsync<ExecuteWorkflowRequest>();
        var command = new ExecuteWorkflowCommand(workflowId, body?.InputData);
        var result = await mediator.Send(command);
        return await req.CreateResultResponseAsync(result, HttpStatusCode.Accepted);
    }

    private sealed record ExecuteWorkflowRequest(string? InputData);
}
