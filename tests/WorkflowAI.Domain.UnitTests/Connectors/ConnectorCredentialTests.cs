using FluentAssertions;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Domain.UnitTests.Connectors;

public class ConnectorCredentialTests
{
    [Fact]
    public void Create_ShouldSetCorrectDefaults()
    {
        var connectorId = ConnectorId.New();
        var credential = ConnectorCredential.Create(
            connectorId, "connector-secret", CredentialType.APIKey, "v1");

        credential.ConnectorId.Should().Be(connectorId);
        credential.KeyVaultSecretName.Should().Be("connector-secret");
        credential.CredentialType.Should().Be(CredentialType.APIKey);
        credential.IssuedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateSecret_ShouldUpdateVersionAndRotationTime()
    {
        var credential = ConnectorCredential.Create(
            ConnectorId.New(), "secret", CredentialType.RefreshToken, "v1", DateTime.UtcNow.AddHours(1));

        credential.UpdateSecret("v2", DateTime.UtcNow.AddHours(2));

        credential.KeyVaultSecretVersion.Should().Be("v2");
        credential.LastRotatedAt.Should().NotBeNull();
    }

    [Fact]
    public void IsExpiringSoon_ShouldReturnTrue_WhenWithinThreshold()
    {
        var credential = ConnectorCredential.Create(
            ConnectorId.New(), "secret", CredentialType.AccessToken, "v1",
            DateTime.UtcNow.AddMinutes(10));

        credential.IsExpiringSoon(TimeSpan.FromMinutes(15)).Should().BeTrue();
    }

    [Fact]
    public void IsExpiringSoon_ShouldReturnFalse_WhenNotWithinThreshold()
    {
        var credential = ConnectorCredential.Create(
            ConnectorId.New(), "secret", CredentialType.AccessToken, "v1",
            DateTime.UtcNow.AddHours(2));

        credential.IsExpiringSoon(TimeSpan.FromMinutes(15)).Should().BeFalse();
    }
}
