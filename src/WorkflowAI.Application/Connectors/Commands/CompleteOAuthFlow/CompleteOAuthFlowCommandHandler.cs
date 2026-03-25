using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.Connectors.Commands.CompleteOAuthFlow;

public sealed class CompleteOAuthFlowCommandHandler(
    IConnectorRepository connectorRepository,
    IConnectorService connectorService,
    IKeyVaultService keyVaultService,
    IApiConnectionProvisioner apiConnectionProvisioner)
    : IRequestHandler<CompleteOAuthFlowCommand, Result>
{
    public async Task<Result> Handle(CompleteOAuthFlowCommand request, CancellationToken ct)
    {
        var (accessToken, refreshToken, expiresAt) = await connectorService.ExchangeOAuthCodeAsync(request.Code, request.State, ct);
        // State contains connectorId
        if (!Guid.TryParse(request.State, out var connectorGuid))
            return Error.Validation("OAuth.InvalidState", "Invalid OAuth state parameter.");

        var connector = await connectorRepository.GetByIdAsync(ConnectorId.From(connectorGuid), ct);
        if (connector is null)
            return Error.NotFound("Connector.NotFound", "Connector not found.");

        var secretName = $"connector-{connector.Id.Value}-refresh-token";
        var (name, version) = await keyVaultService.SetSecretAsync(secretName, refreshToken, expiresAt, ct);
        var credential = ConnectorCredential.Create(connector.Id, name, CredentialType.RefreshToken, version, expiresAt);
        await connectorRepository.AddCredentialAsync(credential, ct);
        connector.SetCredential(credential.Id);

        var apiConnectionId = await apiConnectionProvisioner.ProvisionAsync(connector, ct);
        connector.SetApiConnectionId(apiConnectionId);
        connector.Activate(expiresAt);
        await connectorRepository.UpdateAsync(connector, ct);

        return Result.Success();
    }
}
