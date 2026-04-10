using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.TenantConnectors.Queries.GetTenantConnectorApis;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.TenantConnectors;

public sealed class GetTenantConnectorApisFunction(IMediator mediator)
{
    [Function("GetTenantConnectorApis")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get",
            Route = "tenants/{tenantId}/connectors/{connectorId}/apis")]
        HttpRequestData req,
        Guid tenantId,
        Guid connectorId)
    {
        var query = new GetTenantConnectorApisQuery(connectorId);
        var result = await mediator.Send(query);
        return await req.CreateResultResponseAsync(result);
    }
}
