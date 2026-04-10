using System.Net;
using System.Text.Json;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.TenantConnectors.Commands.ExecuteConnectorApi;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.TenantConnectors;

public sealed class ExecuteConnectorApiFunction(IMediator mediator)
{
    [Function("ExecuteConnectorApi")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post",
            Route = "tenants/{tenantId}/connectors/{connectorId}/apis/{apiId}/execute")]
        HttpRequestData req,
        Guid tenantId,
        Guid connectorId,
        Guid apiId)
    {
        JsonElement? body = null;

        if (req.Body.Length > 0)
        {
            try
            {
                body = await req.ReadFromJsonAsync<JsonElement>();
            }
            catch
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { Message = "Invalid JSON body." });
                return bad;
            }
        }

        var command = new ExecuteConnectorApiCommand(tenantId, connectorId, apiId, body);
        var result = await mediator.Send(command);
        return await req.CreateResultResponseAsync(result);
    }
}
