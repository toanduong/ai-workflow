using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Workflows.Queries.ListWorkflows;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Workflows;

public sealed class ListWorkflowsFunction(IMediator mediator)
{
    [Function("ListWorkflows")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "workflows")]
        HttpRequestData req)
    {
        var result = await mediator.Send(new ListWorkflowsQuery());
        return await req.CreateResultResponseAsync(result);
    }
}
