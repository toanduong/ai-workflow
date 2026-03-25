using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Workflows.Queries.GetWorkflow;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Workflows;

public sealed class GetWorkflowFunction(IMediator mediator)
{
    [Function("GetWorkflow")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "workflows/{workflowId}")]
        HttpRequestData req,
        Guid workflowId)
    {
        var result = await mediator.Send(new GetWorkflowQuery(workflowId));
        return await req.CreateResultResponseAsync(result);
    }
}
