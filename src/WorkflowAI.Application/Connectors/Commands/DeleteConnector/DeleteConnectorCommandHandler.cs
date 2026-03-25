using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.Connectors.Commands.DeleteConnector;

public sealed class DeleteConnectorCommandHandler(
    IConnectorRepository connectorRepository,
    IKeyVaultService keyVaultService,
    IApiConnectionProvisioner apiConnectionProvisioner)
    : IRequestHandler<DeleteConnectorCommand, Result>
{
    public async Task<Result> Handle(DeleteConnectorCommand request, CancellationToken ct)
    {
        var connector = await connectorRepository.GetByIdAsync(ConnectorId.From(request.ConnectorId), ct);
        if (connector is null)
            return Error.NotFound("Connector.NotFound", "Connector not found.");

        if (connector.CredentialId.HasValue)
        {
            var credential = await connectorRepository.GetCredentialAsync(connector.CredentialId.Value, ct);
            if (credential is not null)
            {
                await keyVaultService.DeleteSecretAsync(credential.KeyVaultSecretName, ct);
                await connectorRepository.DeleteCredentialAsync(credential.Id, ct);
            }
        }
        if (connector.AzureApiConnectionId is not null)
            await apiConnectionProvisioner.DeprovisionAsync(connector.AzureApiConnectionId, ct);

        await connectorRepository.DeleteAsync(connector.Id, ct);
        return Result.Success();
    }
}
