using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.Connectors.Commands.CreateConnector;
using WorkflowAI.Application.UnitTests.Common.Fakes;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.UnitTests.Connectors;

public class CreateConnectorCommandHandlerTests
{
    private readonly IConnectorRepository _connectorRepository = Substitute.For<IConnectorRepository>();
    private readonly IConnectorService _connectorService = Substitute.For<IConnectorService>();
    private readonly IKeyVaultService _keyVaultService = Substitute.For<IKeyVaultService>();
    private readonly CurrentUserServiceFake _currentUserService = CurrentUserServiceFake.Authenticated();

    private CreateConnectorCommandHandler CreateHandler() =>
        new(_connectorRepository, _connectorService, _keyVaultService, _currentUserService);

    [Fact]
    public async Task Handle_ShouldCreateApiKeyConnector_AndActivate_WhenSecretProvided()
    {
        _keyVaultService.SetSecretAsync(Arg.Any<string>(), Arg.Any<string>(), ct: Arg.Any<CancellationToken>())
            .Returns(("my-secret", "v1"));
        var command = new CreateConnectorCommand("My API", "Slack", "APIKey", null, null, "my-api-key");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ConnectorId.Should().NotBeEmpty();
        result.Value.OAuthConsentUrl.Should().BeNull();
        await _keyVaultService.Received(1).SetSecretAsync(Arg.Any<string>(), "my-api-key", ct: Arg.Any<CancellationToken>());
        await _connectorRepository.Received(1).AddCredentialAsync(Arg.Any<ConnectorCredential>(), Arg.Any<CancellationToken>());
        await _connectorRepository.Received(1).AddAsync(
            Arg.Is<Connector>(c => c.Status == ConnectorStatus.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnOAuthConsentUrl_WhenAuthModelIsOAuth2()
    {
        _connectorService.GenerateOAuthConsentUrlAsync(Arg.Any<ConnectorId>(), Arg.Any<ConnectorType>(), Arg.Any<CancellationToken>())
            .Returns("https://oauth.example.com/auth");
        var command = new CreateConnectorCommand("My OAuth", "Office365", "OAuth2", null, null, null);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.OAuthConsentUrl.Should().Be("https://oauth.example.com/auth");
        await _keyVaultService.DidNotReceive().SetSecretAsync(Arg.Any<string>(), Arg.Any<string>(), ct: Arg.Any<CancellationToken>());
        await _connectorRepository.DidNotReceive().AddCredentialAsync(Arg.Any<ConnectorCredential>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCreateConnector_ForManagedIdentity_WithNoSecret()
    {
        var command = new CreateConnectorCommand("My MI", "Custom", "ManagedIdentity", null, null, null);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _keyVaultService.DidNotReceive().SetSecretAsync(Arg.Any<string>(), Arg.Any<string>(), ct: Arg.Any<CancellationToken>());
        await _connectorService.DidNotReceive().GenerateOAuthConsentUrlAsync(Arg.Any<ConnectorId>(), Arg.Any<ConnectorType>(), Arg.Any<CancellationToken>());
        await _connectorRepository.Received(1).AddAsync(
            Arg.Is<Connector>(c => c.Status == ConnectorStatus.Created),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenUserNotAuthenticated()
    {
        var unauthenticated = CurrentUserServiceFake.Unauthenticated();
        var handler = new CreateConnectorCommandHandler(_connectorRepository, _connectorService, _keyVaultService, unauthenticated);
        var command = new CreateConnectorCommand("X", "Slack", "APIKey", null, null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Auth.Required");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenConnectorTypeIsInvalid()
    {
        var command = new CreateConnectorCommand("X", "InvalidType", "APIKey", null, null, null);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Connector.InvalidType");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenAuthModelIsInvalid()
    {
        var command = new CreateConnectorCommand("X", "Slack", "InvalidModel", null, null, null);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Connector.InvalidAuthModel");
    }

    [Fact]
    public async Task Handle_ShouldStoreConnectionStringCredential_WhenConnectionStringModel()
    {
        _keyVaultService.SetSecretAsync(Arg.Any<string>(), Arg.Any<string>(), ct: Arg.Any<CancellationToken>())
            .Returns(("conn-secret", "v1"));
        var command = new CreateConnectorCommand("My DB", "Custom", "ConnectionString", null, null, "Server=...;");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _connectorRepository.Received(1).AddCredentialAsync(
            Arg.Is<ConnectorCredential>(c => c.CredentialType == CredentialType.ConnectionString),
            Arg.Any<CancellationToken>());
    }
}
