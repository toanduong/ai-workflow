using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.Connectors.Commands.RefreshConnectorToken;

public sealed class RefreshConnectorTokenCommandHandler(
    IConnectorRepository connectorRepository,
    IConnectorService connectorService,
    IKeyVaultService keyVaultService)
    : IRequestHandler<RefreshConnectorTokenCommand, Result>
{
    public async Task<Result> Handle(RefreshConnectorTokenCommand request, CancellationToken ct)
    {
        var connector = await connectorRepository.GetByIdAsync(ConnectorId.From(request.ConnectorId), ct);
        if (connector is null)
            return Error.NotFound("Connector.NotFound", "Connector not found.");
        if (connector.AuthModel != AuthModel.OAuth2)
            return Error.Validation("Connector.NotOAuth", "Only OAuth2 connectors can be refreshed.");
        if (connector.CredentialId is null)
            return Error.Validation("Connector.NoCredential", "Connector has no credential.");

        var credential = await connectorRepository.GetCredentialAsync(connector.CredentialId.Value, ct);
        if (credential is null)
            return Error.NotFound("Credential.NotFound", "Credential not found.");

        var refreshToken = await keyVaultService.GetSecretAsync(credential.KeyVaultSecretName, credential.KeyVaultSecretVersion, ct);
        var (newAccessToken, expiresAt) = await connectorService.RefreshOAuthTokenAsync(refreshToken, connector.ConnectorType, ct);

        var (name, version) = await keyVaultService.SetSecretAsync(credential.KeyVaultSecretName, newAccessToken, expiresAt, ct);
        credential.UpdateSecret(version, expiresAt);
        await connectorRepository.UpdateCredentialAsync(credential, ct);

        connector.Activate(expiresAt);
        await connectorRepository.UpdateAsync(connector, ct);
        return Result.Success();
    }
}
