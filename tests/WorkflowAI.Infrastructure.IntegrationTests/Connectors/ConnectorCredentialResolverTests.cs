using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Connectors;
using WorkflowAI.Domain.Users;
using WorkflowAI.Infrastructure.Connectors;

namespace WorkflowAI.Infrastructure.IntegrationTests.Connectors;

public class ConnectorCredentialResolverTests
{
    private readonly IConnectorRepository _connectorRepo = Substitute.For<IConnectorRepository>();
    private readonly IKeyVaultService _keyVault = Substitute.For<IKeyVaultService>();

    private ConnectorCredentialResolver CreateResolver() =>
        new(_connectorRepo, _keyVault, NullLogger<ConnectorCredentialResolver>.Instance);

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: Returns null when connector not found
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenConnectorNotFound()
    {
        var connectorId = ConnectorId.New();
        _connectorRepo.GetByIdAsync(connectorId, Arg.Any<CancellationToken>())
            .Returns((Connector?)null);

        var result = await CreateResolver().ResolveAsync(connectorId);

        result.Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2: Returns plain values directly from configuration JSON (no secret)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ResolveAsync_ShouldReturnPlainValues_WhenNoSecretMarker()
    {
        var connectorId = ConnectorId.New();
        var connector = Connector.Create("Pancake", ConnectorType.Custom, AuthModel.APIKey, UserId.New(),
            configuration: """{"page_id":"page123","env":"prod"}""");

        _connectorRepo.GetByIdAsync(connectorId, Arg.Any<CancellationToken>())
            .Returns(connector);

        var result = await CreateResolver().ResolveAsync(connectorId);

        result.Should().NotBeNull();
        result!.Placeholders["page_id"].Should().Be("page123");
        result.Placeholders["env"].Should().Be("prod");
        await _keyVault.DidNotReceive()
            .GetSecretAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3: "$secret" marker replaced with Key Vault secret value
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ResolveAsync_ShouldReplaceSecretMarker_WithKeyVaultValue()
    {
        var connectorId = ConnectorId.New();
        var credentialId = Guid.NewGuid();

        var connector = Connector.Create("Pancake", ConnectorType.Custom, AuthModel.APIKey, UserId.New(),
            configuration: """{"page_id":"page456","page_access_token":"$secret"}""");
        connector.SetCredential(credentialId);

        var credential = ConnectorCredential.Create(
            connector.Id, "pancake-page-access-token", CredentialType.APIKey);

        _connectorRepo.GetByIdAsync(connectorId, Arg.Any<CancellationToken>())
            .Returns(connector);
        _connectorRepo.GetCredentialAsync(credentialId, Arg.Any<CancellationToken>())
            .Returns(credential);
        _keyVault.GetSecretAsync("pancake-page-access-token", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("actual-token-xyz");

        var result = await CreateResolver().ResolveAsync(connectorId);

        result.Should().NotBeNull();
        result!.Placeholders["page_id"].Should().Be("page456");
        result.Placeholders["page_access_token"].Should().Be("actual-token-xyz");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4: "$secret" present but no CredentialId on connector → returns
    //         placeholders with "$secret" left as-is (graceful degradation)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ResolveAsync_ShouldReturnPlaceholdersWithoutSecret_WhenNoCredentialId()
    {
        var connectorId = ConnectorId.New();
        var connector = Connector.Create("Pancake", ConnectorType.Custom, AuthModel.APIKey, UserId.New(),
            configuration: """{"page_access_token":"$secret"}""");
        // CredentialId NOT set

        _connectorRepo.GetByIdAsync(connectorId, Arg.Any<CancellationToken>())
            .Returns(connector);

        var result = await CreateResolver().ResolveAsync(connectorId);

        result.Should().NotBeNull();
        // Secret marker not resolved, but still returns result (graceful)
        result!.Placeholders["page_access_token"].Should().Be("$secret");
        await _keyVault.DidNotReceive()
            .GetSecretAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 5: null/empty configuration → returns empty placeholders
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ResolveAsync_ShouldReturnEmptyPlaceholders_WhenConfigurationIsNull()
    {
        var connectorId = ConnectorId.New();
        var connector = Connector.Create("Empty", ConnectorType.Custom, AuthModel.APIKey, UserId.New());
        // No configuration

        _connectorRepo.GetByIdAsync(connectorId, Arg.Any<CancellationToken>())
            .Returns(connector);

        var result = await CreateResolver().ResolveAsync(connectorId);

        result.Should().NotBeNull();
        result!.Placeholders.Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 6: mixed config — one plain value, one secret
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ResolveAsync_ShouldResolveMixed_WhenBothPlainAndSecretPresent()
    {
        var connectorId = ConnectorId.New();
        var credentialId = Guid.NewGuid();

        var connector = Connector.Create("Pancake", ConnectorType.Custom, AuthModel.APIKey, UserId.New(),
            configuration: """{"page_id":"static-page","page_access_token":"$secret","env":"prod"}""");
        connector.SetCredential(credentialId);

        var credential = ConnectorCredential.Create(
            connector.Id, "my-secret-name", CredentialType.APIKey);

        _connectorRepo.GetByIdAsync(connectorId, Arg.Any<CancellationToken>())
            .Returns(connector);
        _connectorRepo.GetCredentialAsync(credentialId, Arg.Any<CancellationToken>())
            .Returns(credential);
        _keyVault.GetSecretAsync("my-secret-name", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("resolved-token");

        var result = await CreateResolver().ResolveAsync(connectorId);

        result!.Placeholders["page_id"].Should().Be("static-page");
        result.Placeholders["page_access_token"].Should().Be("resolved-token");
        result.Placeholders["env"].Should().Be("prod");
    }
}
