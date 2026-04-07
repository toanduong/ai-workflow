using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.TenantConnectors.Commands.DeleteTenantConnector;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.UnitTests.TenantConnectors;

public class DeleteTenantConnectorCommandHandlerTests
{
    private readonly ITenantConnectorRepository _repository =
        Substitute.For<ITenantConnectorRepository>();

    private DeleteTenantConnectorCommandHandler CreateHandler() =>
        new(_repository);

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenConnectorDoesNotExist()
    {
        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);

        var result = await CreateHandler().Handle(
            new DeleteTenantConnectorCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.NotFound");
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldDeleteApisBeforeConnector()
    {
        var connector = new TenantConnectorBuilder().WithConnectorName("Apollo").Activated().Build();
        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(connector);

        var result = await CreateHandler().Handle(
            new DeleteTenantConnectorCommand(connector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        // Table 2 must be cleared before Table 1 to respect FK constraint
        Received.InOrder(() =>
        {
            _repository.DeleteApisByConnectorAsync(connector.Id, Arg.Any<CancellationToken>());
            _repository.DeleteAsync(connector.Id, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_ShouldDeletePendingConnector_NotJustActive()
    {
        var connector = new TenantConnectorBuilder().WithConnectorName("HubSpot").Build(); // Pending
        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(connector);

        var result = await CreateHandler().Handle(
            new DeleteTenantConnectorCommand(connector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).DeleteAsync(connector.Id, Arg.Any<CancellationToken>());
    }
}
