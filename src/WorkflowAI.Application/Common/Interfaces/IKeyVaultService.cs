namespace WorkflowAI.Application.Common.Interfaces;

public interface IKeyVaultService
{
    Task<string> GetSecretAsync(string secretName, string? version = null, CancellationToken ct = default);
    Task<(string SecretName, string Version)> SetSecretAsync(string secretName, string value, DateTime? expiresAt = null, CancellationToken ct = default);
    Task DeleteSecretAsync(string secretName, CancellationToken ct = default);
}
