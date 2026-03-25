using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Workflows.Commands.DeleteWorkflow;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Workflows;

public sealed class DeleteWorkflowFunction(IMediator mediator)
{
    [Function("DeleteWorkflow")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "workflows/{workflowId}")]
        HttpRequestData req,
        Guid workflowId)
    {
        var result = await mediator.Send(new DeleteWorkflowCommand(workflowId));
        return await req.CreateResultResponseAsync(result);
    }
}
