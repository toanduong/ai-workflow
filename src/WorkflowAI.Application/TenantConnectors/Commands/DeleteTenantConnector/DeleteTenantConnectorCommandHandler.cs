using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.DeleteTenantConnector;

public sealed class DeleteTenantConnectorCommandHandler(
    ITenantConnectorRepository repository,
    IKeyVaultService keyVaultService,
    ICurrentUserService currentUserService,
    ILogger<DeleteTenantConnectorCommandHandler> logger)
    : IRequestHandler<DeleteTenantConnectorCommand, Result>
{
    public async Task<Result> Handle(
        DeleteTenantConnectorCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated)
            return Error.Unauthorized("Auth.Required", "User must be authenticated.");

        var id = TenantConnectorId.From(request.TenantConnectorId);
        var connector = await repository.GetByIdAsync(id, cancellationToken);

        if (connector is null)
            return Error.NotFound("TenantConnector.NotFound",
                $"TenantConnector {request.TenantConnectorId} not found.");

        await DeleteCredentialsAsync(connector, cancellationToken);
        await repository.DeleteAsync(id, cancellationToken);
        return Result.Success();
    }

    private async Task DeleteCredentialsAsync(TenantConnector connector, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(connector.CredentialSecretNames))
            return;

        try
        {
            var secretNames = JsonSerializer.Deserialize<Dictionary<string, string>>(
                connector.CredentialSecretNames);

            if (secretNames is null) return;

            foreach (var secretName in secretNames.Values)
            {
                await keyVaultService.DeleteSecretAsync(secretName, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail the delete — the DB record should still be removed
            // even if Key Vault cleanup fails (orphaned secrets can be cleaned up separately)
            logger.LogWarning(ex,
                "Failed to delete Key Vault secrets for connector {ConnectorId}. Proceeding with DB delete.",
                connector.Id.Value);
        }
    }
}
