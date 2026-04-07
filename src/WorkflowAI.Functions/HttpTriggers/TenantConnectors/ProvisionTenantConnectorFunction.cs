using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.TenantConnectors.Commands.ProvisionTenantConnector;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.TenantConnectors;

public sealed class ProvisionTenantConnectorFunction(IMediator mediator)
{
    [Function("ProvisionTenantConnector")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants/{tenantId}/connectors")]
        HttpRequestData req,
        string tenantId)
    {
        var body = await req.ReadFromJsonAsync<ProvisionTenantConnectorRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.ConnectorName))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { Message = "connectorName is required." });
            return bad;
        }

        var command = new ProvisionTenantConnectorCommand(Guid.Parse(tenantId), body.ConnectorName);
        var result = await mediator.Send(command);
        return await req.CreateResultResponseAsync(result, HttpStatusCode.Created);
    }

    private sealed record ProvisionTenantConnectorRequest(string ConnectorName);
}
