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
        Guid tenantId)
    {
        var body = await req.ReadFromJsonAsync<ProvisionTenantConnectorRequest>();
        if (body is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { Message = "Invalid request body." });
            return badRequest;
        }

        var command = new ProvisionTenantConnectorCommand(tenantId, body.ConnectorType);
        var result = await mediator.Send(command);
        return await req.CreateResultResponseAsync(result, HttpStatusCode.Created);
    }
}

public sealed record ProvisionTenantConnectorRequest(string ConnectorType);
