using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.TenantConnectors.Commands.GenerateMcpWorkflows;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.TenantConnectors;

public sealed class GenerateMcpWorkflowsFunction(IMediator mediator)
{
    [Function("GenerateMcpWorkflows")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants/{tenantId}/connectors/{id}/generate")]
        HttpRequestData req,
        string tenantId,
        string id)
    {
        var command = new GenerateMcpWorkflowsCommand(Guid.Parse(id));
        var result = await mediator.Send(command);
        return await req.CreateResultResponseAsync(result);
    }
}
