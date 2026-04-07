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
        Guid tenantId,
        Guid id)
    {
        var body = await req.ReadFromJsonAsync<ValidateTenantConnectorRequest>();
        if (body is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { Message = "Invalid request body." });
            return badRequest;
        }

        var command = new ValidateTenantConnectorCommand(id, body.CredentialFields);
        var result = await mediator.Send(command);
        return await req.CreateResultResponseAsync(result);
    }
}

public sealed record ValidateTenantConnectorRequest(Dictionary<string, string> CredentialFields);
