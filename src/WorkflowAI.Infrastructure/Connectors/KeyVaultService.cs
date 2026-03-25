using Azure.Security.KeyVault.Secrets;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors;

public sealed class KeyVaultService(SecretClient secretClient) : IKeyVaultService
{
    public async Task<string> GetSecretAsync(string secretName, string? version = null, CancellationToken ct = default)
    {
        var response = await secretClient.GetSecretAsync(secretName, version, ct);
        return response.Value.Value;
    }

    public async Task<(string SecretName, string Version)> SetSecretAsync(
        string secretName, string value, DateTime? expiresAt = null, CancellationToken ct = default)
    {
        var secret = new KeyVaultSecret(secretName, value);
        if (expiresAt.HasValue)
            secret.Properties.ExpiresOn = expiresAt.Value;

        var response = await secretClient.SetSecretAsync(secret, ct);
        return (response.Value.Name, response.Value.Properties.Version);
    }

    public async Task DeleteSecretAsync(string secretName, CancellationToken ct = default)
    {
        await secretClient.StartDeleteSecretAsync(secretName, ct);
    }
}
