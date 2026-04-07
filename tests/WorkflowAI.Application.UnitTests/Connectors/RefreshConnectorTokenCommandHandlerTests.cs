using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.Connectors.Commands.RefreshConnectorToken;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.UnitTests.Connectors;

public class RefreshConnectorTokenCommandHandlerTests
{
    private readonly IConnectorRepository _connectorRepository = Substitute.For<IConnectorRepository>();
    private readonly IConnectorService _connectorService = Substitute.For<IConnectorService>();
    private readonly IKeyVaultService _keyVaultService = Substitute.For<IKeyVaultService>();

    private RefreshConnectorTokenCommandHandler CreateHandler() =>
        new(_connectorRepository, _connectorService, _keyVaultService);

    [Fact]
    public async Task Handle_ShouldRefreshToken_AndUpdateConnector_WhenOAuth2ConnectorHasCredential()
    {
        var credentialId = Guid.NewGuid();
        var connector = new ConnectorBuilder()
            .WithAuthModel(AuthModel.OAuth2)
            .WithCredentialId(credentialId)
            .Activated().Build();
        var credential = ConnectorCredential.Create(connector.Id, "my-secret", CredentialType.RefreshToken, "v1");
        var newExpiry = DateTime.UtcNow.AddHours(1);
        _connectorRepository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>()).Returns(connector);
        _connectorRepository.GetCredentialAsync(credentialId, Arg.Any<CancellationToken>()).Returns(credential);
        _keyVaultService.GetSecretAsync(credential.KeyVaultSecretName, credential.KeyVaultSecretVersion, Arg.Any<CancellationToken>())
            .Returns("old-refresh-token");
        _connectorService.RefreshOAuthTokenAsync("old-refresh-token", connector.ConnectorType, Arg.Any<CancellationToken>())
            .Returns(("new-access-token", newExpiry));
        _keyVaultService.SetSecretAsync(credential.KeyVaultSecretName, "new-access-token", newExpiry, Arg.Any<CancellationToken>())
            .Returns((credential.KeyVaultSecretName, "v2"));

        var result = await CreateHandler().Handle(new RefreshConnectorTokenCommand(connector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _connectorRepository.Received(1).UpdateCredentialAsync(Arg.Any<ConnectorCredential>(), Arg.Any<CancellationToken>());
        await _connectorRepository.Received(1).UpdateAsync(
            Arg.Is<Connector>(c => c.Status == ConnectorStatus.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenConnectorNotFound()
    {
        _connectorRepository.GetByIdAsync(Arg.Any<ConnectorId>(), Arg.Any<CancellationToken>())
            .Returns((Connector?)null);

        var result = await CreateHandler().Handle(new RefreshConnectorTokenCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Connector.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenConnectorIsNotOAuth2()
    {
        var connector = new ConnectorBuilder().WithAuthModel(AuthModel.APIKey).Build();
        _connectorRepository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>()).Returns(connector);

        var result = await CreateHandler().Handle(new RefreshConnectorTokenCommand(connector.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Connector.NotOAuth");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenConnectorHasNoCredential()
    {
        var connector = new ConnectorBuilder().WithAuthModel(AuthModel.OAuth2).Build();
        _connectorRepository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>()).Returns(connector);

        var result = await CreateHandler().Handle(new RefreshConnectorTokenCommand(connector.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Connector.NoCredential");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenCredentialNotFoundInRepo()
    {
        var credentialId = Guid.NewGuid();
        var connector = new ConnectorBuilder()
            .WithAuthModel(AuthModel.OAuth2)
            .WithCredentialId(credentialId).Build();
        _connectorRepository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>()).Returns(connector);
        _connectorRepository.GetCredentialAsync(credentialId, Arg.Any<CancellationToken>())
            .Returns((ConnectorCredential?)null);

        var result = await CreateHandler().Handle(new RefreshConnectorTokenCommand(connector.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Credential.NotFound");
    }
}
