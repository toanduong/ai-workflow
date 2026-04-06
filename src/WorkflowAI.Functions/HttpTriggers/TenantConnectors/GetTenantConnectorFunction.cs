using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.TenantConnectors.Queries.GetTenantConnector;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.TenantConnectors;

public sealed class GetTenantConnectorFunction(IMediator mediator)
{
    [Function("GetTenantConnector")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId}/connectors/{id}")]
        HttpRequestData req,
        Guid tenantId,
        Guid id)
    {
        var query = new GetTenantConnectorQuery(id);
        var result = await mediator.Send(query);
        return await req.CreateResultResponseAsync(result);
    }
}
