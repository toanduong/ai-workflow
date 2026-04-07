using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors;

/// <summary>
/// No-op Key Vault used when KeyVault:Uri is not configured (local dev).
/// Writes are silently ignored so validation and deletion flows work locally.
/// Reads throw — if a workflow tries to resolve credentials in dev, it fails loudly.
/// </summary>
public sealed class NoOpKeyVaultService : IKeyVaultService
{
    public Task<string> GetSecretAsync(string secretName, string? version = null, CancellationToken ct = default)
        => throw new InvalidOperationException(
            $"Key Vault is not configured. Set 'KeyVault:Uri' in configuration. Secret requested: '{secretName}'");

    public Task<(string SecretName, string Version)> SetSecretAsync(
        string secretName, string value, DateTime? expiresAt = null, CancellationToken ct = default)
        => Task.FromResult((secretName, "no-op"));

    public Task DeleteSecretAsync(string secretName, CancellationToken ct = default)
        => Task.CompletedTask;
}
