using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.Common.Interfaces;

public interface IApiConnectionProvisioner
{
    Task<string> ProvisionAsync(Connector connector, CancellationToken ct = default);
    Task DeprovisionAsync(string azureApiConnectionId, CancellationToken ct = default);
}
