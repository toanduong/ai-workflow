using System.Text.Json;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Infrastructure.Connectors;

/// <summary>
/// Resolves placeholder values for connector-based HTTP step execution.
///
/// Convention for Connector.Configuration JSON:
///   - Plain string values are used directly as placeholder replacements.
///   - Values equal to "$secret" are replaced with the credential secret fetched from Key Vault.
///
/// Example:
///   Connector.Configuration = { "page_id": "123456", "page_access_token": "$secret" }
///   Credential.KeyVaultSecretName = "pancake-page-access-token"
///
///   Resolved placeholders: { "page_id": "123456", "page_access_token": "<kv-secret-value>" }
/// </summary>
public sealed class ConnectorCredentialResolver(
    IConnectorRepository connectorRepository,
    IKeyVaultService keyVaultService,
    ILogger<ConnectorCredentialResolver> logger) : IConnectorCredentialResolver
{
    private const string SecretMarker = "$secret";

    public async Task<ConnectorCredentials?> ResolveAsync(ConnectorId connectorId, CancellationToken ct = default)
    {
        var connector = await connectorRepository.GetByIdAsync(connectorId, ct);
        if (connector is null)
        {
            logger.LogWarning("Connector {ConnectorId} not found", connectorId.Value);
            return null;
        }

        var placeholders = ParseConfiguration(connector.Configuration);

        // Replace "$secret" markers with the actual secret from Key Vault
        var secretKeys = placeholders
            .Where(kv => kv.Value == SecretMarker)
            .Select(kv => kv.Key)
            .ToList();

        if (secretKeys.Count > 0)
        {
            if (connector.CredentialId is null)
            {
                logger.LogWarning(
                    "Connector {ConnectorId} has '$secret' placeholders but no CredentialId set",
                    connectorId.Value);
            }
            else
            {
                var credential = await connectorRepository.GetCredentialAsync(connector.CredentialId.Value, ct);
                if (credential is null)
                {
                    logger.LogWarning(
                        "Credential {CredentialId} for connector {ConnectorId} not found",
                        connector.CredentialId.Value, connectorId.Value);
                }
                else
                {
                    var secret = await keyVaultService.GetSecretAsync(
                        credential.KeyVaultSecretName,
                        credential.KeyVaultSecretVersion,
                        ct);

                    foreach (var key in secretKeys)
                        placeholders[key] = secret;
                }
            }
        }

        return new ConnectorCredentials(placeholders);
    }

    private static Dictionary<string, string> ParseConfiguration(string? configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration))
            return [];

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(
                configuration,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return parsed ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
