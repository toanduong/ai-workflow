using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.Connectors.Commands.CreateConnector;

public sealed class CreateConnectorCommandHandler(
    IConnectorRepository connectorRepository,
    IConnectorService connectorService,
    IKeyVaultService keyVaultService,
    ICurrentUserService currentUserService)
    : IRequestHandler<CreateConnectorCommand, Result<CreateConnectorResult>>
{
    public async Task<Result<CreateConnectorResult>> Handle(CreateConnectorCommand request, CancellationToken ct)
    {
        if (currentUserService.UserId is null)
            return Error.Unauthorized("Auth.Required", "User must be authenticated.");

        var connectorType = ConnectorType.FromName(request.ConnectorType);
        if (connectorType is null)
            return Error.Validation("Connector.InvalidType", $"Invalid connector type: {request.ConnectorType}");

        var authModel = AuthModel.FromName(request.AuthModel);
        if (authModel is null)
            return Error.Validation("Connector.InvalidAuthModel", $"Invalid auth model: {request.AuthModel}");

        var connector = Connector.Create(
            request.Name, connectorType, authModel,
            currentUserService.UserId.Value, request.Configuration, request.ManagedApiId);

        string? oauthConsentUrl = null;

        if (authModel == AuthModel.OAuth2)
        {
            oauthConsentUrl = await connectorService.GenerateOAuthConsentUrlAsync(connector.Id, connectorType, ct);
        }
        else if (request.Secret is not null)
        {
            var secretName = $"connector-{connector.Id.Value}-secret";
            var (name, version) = await keyVaultService.SetSecretAsync(secretName, request.Secret, ct: ct);
            var credentialType = authModel == AuthModel.APIKey ? CredentialType.APIKey : CredentialType.ConnectionString;
            var credential = ConnectorCredential.Create(connector.Id, name, credentialType, version);
            await connectorRepository.AddCredentialAsync(credential, ct);
            connector.SetCredential(credential.Id);
            connector.Activate();
        }

        await connectorRepository.AddAsync(connector, ct);
        return new CreateConnectorResult(connector.Id.Value, oauthConsentUrl);
    }
}
