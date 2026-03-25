using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Connectors;

public sealed class ConnectorCredential : Entity<Guid>
{
    public ConnectorId ConnectorId { get; private set; }
    public string KeyVaultSecretName { get; private set; } = string.Empty;
    public string? KeyVaultSecretVersion { get; private set; }
    public CredentialType CredentialType { get; private set; } = CredentialType.APIKey;
    public DateTime? IssuedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? LastRotatedAt { get; private set; }

    private ConnectorCredential() { }

    public static ConnectorCredential Create(
        ConnectorId connectorId,
        string keyVaultSecretName,
        CredentialType credentialType,
        string? keyVaultSecretVersion = null,
        DateTime? expiresAt = null)
    {
        return new ConnectorCredential
        {
            Id = Guid.NewGuid(),
            ConnectorId = connectorId,
            KeyVaultSecretName = keyVaultSecretName,
            KeyVaultSecretVersion = keyVaultSecretVersion,
            CredentialType = credentialType,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };
    }

    public void UpdateSecret(string secretVersion, DateTime? expiresAt)
    {
        KeyVaultSecretVersion = secretVersion;
        ExpiresAt = expiresAt;
        LastRotatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsExpiringSoon(TimeSpan threshold) =>
        ExpiresAt.HasValue && DateTime.UtcNow.Add(threshold) >= ExpiresAt.Value;
}
