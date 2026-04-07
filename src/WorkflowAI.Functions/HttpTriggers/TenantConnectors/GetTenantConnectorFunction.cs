using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.TenantConnectors.Queries.GetTenantConnector;
using WorkflowAI.Application.TenantConnectors.Queries.GetTenantConnectorApis;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.TenantConnectors;

public sealed class GetTenantConnectorFunction(IMediator mediator)
{
    [Function("GetTenantConnector")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId}/connectors/{id}")]
        HttpRequestData req,
        string tenantId,
        string id)
    {
        var query = new GetTenantConnectorQuery(Guid.Parse(id));
        var result = await mediator.Send(query);
        return await req.CreateResultResponseAsync(result);
    }

    [Function("GetTenantConnectorApis")]
    public async Task<HttpResponseData> GetApis(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId}/connectors/{id}/apis")]
        HttpRequestData req,
        string tenantId,
        string id)
    {
        var query = new GetTenantConnectorApisQuery(Guid.Parse(id));
        var result = await mediator.Send(query);
        return await req.CreateResultResponseAsync(result);
    }
}
