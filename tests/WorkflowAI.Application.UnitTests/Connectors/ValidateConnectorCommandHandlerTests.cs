using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.Connectors.Commands.ValidateConnector;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.UnitTests.Connectors;

public class ValidateConnectorCommandHandlerTests
{
    private readonly IConnectorRepository _connectorRepository = Substitute.For<IConnectorRepository>();
    private readonly IConnectorService _connectorService = Substitute.For<IConnectorService>();

    private ValidateConnectorCommandHandler CreateHandler() =>
        new(_connectorRepository, _connectorService);

    [Fact]
    public async Task Handle_ShouldActivateConnector_WhenValidationPasses()
    {
        var connector = new ConnectorBuilder().Build();
        _connectorRepository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>()).Returns(connector);
        _connectorService.ValidateConnectorAsync(connector, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().Handle(new ValidateConnectorCommand(connector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        await _connectorRepository.Received(1).UpdateAsync(
            Arg.Is<Connector>(c => c.Status == ConnectorStatus.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldMarkConnectorFailed_WhenValidationFails()
    {
        var connector = new ConnectorBuilder().Build();
        _connectorRepository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>()).Returns(connector);
        _connectorService.ValidateConnectorAsync(connector, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(new ValidateConnectorCommand(connector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        await _connectorRepository.Received(1).UpdateAsync(
            Arg.Is<Connector>(c => c.Status == ConnectorStatus.Failed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSetStatusToValidating_BeforeServiceCall()
    {
        var connector = new ConnectorBuilder().Build();
        _connectorRepository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>()).Returns(connector);

        ConnectorStatus? statusDuringValidation = null;
        _connectorService.ValidateConnectorAsync(Arg.Any<Connector>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                statusDuringValidation = callInfo.Arg<Connector>().Status;
                return true;
            });

        await CreateHandler().Handle(new ValidateConnectorCommand(connector.Id.Value), CancellationToken.None);

        statusDuringValidation.Should().Be(ConnectorStatus.Validating);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenConnectorNotFound()
    {
        _connectorRepository.GetByIdAsync(Arg.Any<ConnectorId>(), Arg.Any<CancellationToken>())
            .Returns((Connector?)null);

        var result = await CreateHandler().Handle(new ValidateConnectorCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Connector.NotFound");
    }
}
