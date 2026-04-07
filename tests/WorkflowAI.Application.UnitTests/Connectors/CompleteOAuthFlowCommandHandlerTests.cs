using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.Connectors.Commands.CompleteOAuthFlow;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.UnitTests.Connectors;

public class CompleteOAuthFlowCommandHandlerTests
{
    private readonly IConnectorRepository _connectorRepository = Substitute.For<IConnectorRepository>();
    private readonly IConnectorService _connectorService = Substitute.For<IConnectorService>();
    private readonly IKeyVaultService _keyVaultService = Substitute.For<IKeyVaultService>();
    private readonly IApiConnectionProvisioner _apiConnectionProvisioner = Substitute.For<IApiConnectionProvisioner>();

    private CompleteOAuthFlowCommandHandler CreateHandler() =>
        new(_connectorRepository, _connectorService, _keyVaultService, _apiConnectionProvisioner);

    [Fact]
    public async Task Handle_ShouldActivateConnector_WhenOAuthCodeExchangeSucceeds()
    {
        var connector = new ConnectorBuilder().WithAuthModel(AuthModel.OAuth2).Build();
        var expiresAt = DateTime.UtcNow.AddHours(1);
        _connectorService.ExchangeOAuthCodeAsync("auth-code", connector.Id.Value.ToString(), Arg.Any<CancellationToken>())
            .Returns(("access-token", "refresh-token", expiresAt));
        _keyVaultService.SetSecretAsync(Arg.Any<string>(), "refresh-token", expiresAt, Arg.Any<CancellationToken>())
            .Returns(("connector-secret", "v1"));
        _apiConnectionProvisioner.ProvisionAsync(Arg.Any<Connector>(), Arg.Any<CancellationToken>())
            .Returns("api-conn-456");
        _connectorRepository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>()).Returns(connector);

        var result = await CreateHandler().Handle(
            new CompleteOAuthFlowCommand("auth-code", connector.Id.Value.ToString()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _connectorRepository.Received(1).AddCredentialAsync(Arg.Any<ConnectorCredential>(), Arg.Any<CancellationToken>());
        await _connectorRepository.Received(1).UpdateAsync(
            Arg.Is<Connector>(c => c.Status == ConnectorStatus.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenStateIsNotValidGuid()
    {
        _connectorService.ExchangeOAuthCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(("access", "refresh", DateTime.UtcNow.AddHours(1)));

        var result = await CreateHandler().Handle(
            new CompleteOAuthFlowCommand("code", "not-a-guid"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("OAuth.InvalidState");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenConnectorNotFound()
    {
        var connectorId = Guid.NewGuid();
        _connectorService.ExchangeOAuthCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(("access", "refresh", DateTime.UtcNow.AddHours(1)));
        _connectorRepository.GetByIdAsync(Arg.Any<ConnectorId>(), Arg.Any<CancellationToken>())
            .Returns((Connector?)null);

        var result = await CreateHandler().Handle(
            new CompleteOAuthFlowCommand("code", connectorId.ToString()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Connector.NotFound");
    }
}
