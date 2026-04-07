using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.TenantConnectors.Commands.DeleteTenantConnector;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.TenantConnectors;

public sealed class DeleteTenantConnectorFunction(IMediator mediator)
{
    [Function("DeleteTenantConnector")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tenants/{tenantId}/connectors/{id}")]
        HttpRequestData req,
        Guid tenantId,
        Guid id)
    {
        var command = new DeleteTenantConnectorCommand(id);
        var result = await mediator.Send(command);
        return await req.CreateResultResponseAsync(result);
    }
}
