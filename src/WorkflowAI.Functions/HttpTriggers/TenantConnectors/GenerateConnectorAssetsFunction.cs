using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.TenantConnectors.Commands.GenerateConnectorAssets;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.TenantConnectors;

public sealed class GenerateConnectorAssetsFunction(IMediator mediator)
{
    [Function("GenerateConnectorAssets")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post",
            Route = "tenants/{tenantId}/connectors/{id}/generate")]
        HttpRequestData req,
        Guid tenantId,
        Guid id)
    {
        var command = new GenerateConnectorAssetsCommand(id);
        var result = await mediator.Send(command);
        return await req.CreateResultResponseAsync(result);
    }
}
