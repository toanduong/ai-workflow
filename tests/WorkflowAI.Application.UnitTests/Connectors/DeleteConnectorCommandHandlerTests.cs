using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.Connectors.Commands.DeleteConnector;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.UnitTests.Connectors;

public class DeleteConnectorCommandHandlerTests
{
    private readonly IConnectorRepository _connectorRepository = Substitute.For<IConnectorRepository>();
    private readonly IKeyVaultService _keyVaultService = Substitute.For<IKeyVaultService>();
    private readonly IApiConnectionProvisioner _apiConnectionProvisioner = Substitute.For<IApiConnectionProvisioner>();

    private DeleteConnectorCommandHandler CreateHandler() =>
        new(_connectorRepository, _keyVaultService, _apiConnectionProvisioner);

    [Fact]
    public async Task Handle_ShouldDeleteConnectorWithCredentialAndApiConnection_WhenBothExist()
    {
        var credentialId = Guid.NewGuid();
        var connector = new ConnectorBuilder()
            .WithCredentialId(credentialId)
            .WithApiConnectionId("api-conn-123")
            .Activated().Build();
        var credential = ConnectorCredential.Create(connector.Id, "my-secret", CredentialType.APIKey, "v1");
        _connectorRepository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>()).Returns(connector);
        _connectorRepository.GetCredentialAsync(credentialId, Arg.Any<CancellationToken>()).Returns(credential);

        var result = await CreateHandler().Handle(new DeleteConnectorCommand(connector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _keyVaultService.Received(1).DeleteSecretAsync(credential.KeyVaultSecretName, Arg.Any<CancellationToken>());
        await _connectorRepository.Received(1).DeleteCredentialAsync(credential.Id, Arg.Any<CancellationToken>());
        await _apiConnectionProvisioner.Received(1).DeprovisionAsync("api-conn-123", Arg.Any<CancellationToken>());
        await _connectorRepository.Received(1).DeleteAsync(connector.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldDeleteConnector_WithoutCredential_WhenNoneSet()
    {
        var connector = new ConnectorBuilder().Build();
        _connectorRepository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>()).Returns(connector);

        var result = await CreateHandler().Handle(new DeleteConnectorCommand(connector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _keyVaultService.DidNotReceive().DeleteSecretAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _apiConnectionProvisioner.DidNotReceive().DeprovisionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _connectorRepository.Received(1).DeleteAsync(connector.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSkipKeyVaultDeletion_WhenCredentialNotFoundInRepo()
    {
        var credentialId = Guid.NewGuid();
        var connector = new ConnectorBuilder().WithCredentialId(credentialId).Build();
        _connectorRepository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>()).Returns(connector);
        _connectorRepository.GetCredentialAsync(credentialId, Arg.Any<CancellationToken>())
            .Returns((ConnectorCredential?)null);

        var result = await CreateHandler().Handle(new DeleteConnectorCommand(connector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _keyVaultService.DidNotReceive().DeleteSecretAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _connectorRepository.Received(1).DeleteAsync(connector.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenConnectorNotFound()
    {
        _connectorRepository.GetByIdAsync(Arg.Any<ConnectorId>(), Arg.Any<CancellationToken>())
            .Returns((Connector?)null);

        var result = await CreateHandler().Handle(new DeleteConnectorCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Connector.NotFound");
    }
}
