using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.Common.Interfaces;

/// <summary>
/// Resolves URL placeholder values for a connector.
/// Non-secret values come from connector.Configuration JSON.
/// Secret values (marked with "$secret") are fetched from Key Vault.
///
/// Example connector.Configuration:
/// { "page_id": "123456789", "page_access_token": "$secret" }
///
/// At runtime, "$secret" is replaced with the actual KV secret value so the
/// resulting dictionary can be used to replace {page_id} and {page_access_token}
/// in a step's configuration URL.
/// </summary>
public sealed record ConnectorCredentials(IReadOnlyDictionary<string, string> Placeholders);

public interface IConnectorCredentialResolver
{
    Task<ConnectorCredentials?> ResolveAsync(ConnectorId connectorId, CancellationToken ct = default);
}
