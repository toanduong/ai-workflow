using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Domain.TenantConnectors;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.TenantConnectors;

public sealed class DeleteTenantConnectorFunction(ITenantConnectorRepository repository)
{
    [Function("DeleteTenantConnector")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tenants/{tenantId}/connectors/{id}")]
        HttpRequestData req,
        Guid tenantId,
        Guid id)
    {
        await repository.DeleteAsync(TenantConnectorId.From(id));
        var response = req.CreateResponse(HttpStatusCode.NoContent);
        return response;
    }
}
