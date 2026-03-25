using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors;

public sealed class NoOpKeyVaultService : IKeyVaultService
{
    public Task<string> GetSecretAsync(string secretName, string? version = null, CancellationToken ct = default)
        => throw new InvalidOperationException("Key Vault is not configured. Set 'KeyVault:Uri' in configuration.");

    public Task<(string SecretName, string Version)> SetSecretAsync(
        string secretName, string value, DateTime? expiresAt = null, CancellationToken ct = default)
        => throw new InvalidOperationException("Key Vault is not configured. Set 'KeyVault:Uri' in configuration.");

    public Task DeleteSecretAsync(string secretName, CancellationToken ct = default)
        => throw new InvalidOperationException("Key Vault is not configured. Set 'KeyVault:Uri' in configuration.");
}
