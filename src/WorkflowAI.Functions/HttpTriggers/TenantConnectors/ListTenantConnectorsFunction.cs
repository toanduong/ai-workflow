using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.TenantConnectors.Queries.ListTenantConnectors;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.TenantConnectors;

public sealed class ListTenantConnectorsFunction(IMediator mediator)
{
    [Function("ListTenantConnectors")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId}/connectors")]
        HttpRequestData req,
        Guid tenantId)
    {
        var query = new ListTenantConnectorsQuery(tenantId);
        var result = await mediator.Send(query);
        return await req.CreateResultResponseAsync(result);
    }
}
