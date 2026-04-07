using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.TenantConnectors.Commands.ValidateTenantConnector;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.TenantConnectors;

public sealed class ValidateTenantConnectorFunction(IMediator mediator)
{
    [Function("ValidateTenantConnector")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants/{tenantId}/connectors/{id}/validate")]
        HttpRequestData req,
        string tenantId,
        string id)
    {
        var body = await req.ReadFromJsonAsync<ValidateTenantConnectorRequest>();
        if (body?.Credentials is null || body.Credentials.Count == 0)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { Message = "credentials are required." });
            return bad;
        }

        var command = new ValidateTenantConnectorCommand(Guid.Parse(id), body.Credentials);
        var result = await mediator.Send(command);
        return await req.CreateResultResponseAsync(result);
    }

    // Caller passes a flat key/value map whose keys match the connector's requiredFields.
    // Example for an API-key connector: { "api_key": "abc123" }
    // Example for a self-hosted connector: { "api_key": "abc123", "instance_url": "https://mycompany.example.com" }
    private sealed record ValidateTenantConnectorRequest(
        Dictionary<string, string>? Credentials);
}
