using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Infrastructure.Connectors;

public sealed class ApiConnectionProvisioner : IApiConnectionProvisioner
{
    public Task<string> ProvisionAsync(Connector connector, CancellationToken ct = default)
    {
        // TODO: Use Azure.ResourceManager to create Microsoft.Web/connections resource
        // For now, return a placeholder resource ID
        var resourceId = $"/subscriptions/{{sub}}/resourceGroups/{{rg}}/providers/Microsoft.Web/connections/{connector.ConnectorType.Name.ToLowerInvariant()}-{connector.Id.Value}";
        return Task.FromResult(resourceId);
    }

    public Task DeprovisionAsync(string azureApiConnectionId, CancellationToken ct = default)
    {
        // TODO: Use Azure.ResourceManager to delete the API Connection resource
        return Task.CompletedTask;
    }
}
