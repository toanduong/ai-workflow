using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Connectors.Queries.GetConnector;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.UnitTests.Connectors;

public class GetConnectorQueryHandlerTests
{
    private readonly IConnectorRepository _connectorRepository = Substitute.For<IConnectorRepository>();

    private GetConnectorQueryHandler CreateHandler() => new(_connectorRepository);

    [Fact]
    public async Task Handle_ShouldReturnConnectorDto_WhenFound()
    {
        var connector = new ConnectorBuilder()
            .WithName("My Connector")
            .WithType(ConnectorType.Slack)
            .WithAuthModel(AuthModel.APIKey)
            .Activated().Build();
        _connectorRepository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>()).Returns(connector);

        var result = await CreateHandler().Handle(new GetConnectorQuery(connector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("My Connector");
        result.Value.ConnectorType.Should().Be("Slack");
        result.Value.AuthModel.Should().Be("APIKey");
        result.Value.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenConnectorNotFound()
    {
        _connectorRepository.GetByIdAsync(Arg.Any<ConnectorId>(), Arg.Any<CancellationToken>())
            .Returns((Connector?)null);

        var result = await CreateHandler().Handle(new GetConnectorQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Connector.NotFound");
    }
}
