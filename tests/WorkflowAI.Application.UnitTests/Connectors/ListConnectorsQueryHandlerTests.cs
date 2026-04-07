using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Connectors.Queries.ListConnectors;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.UnitTests.Connectors;

public class ListConnectorsQueryHandlerTests
{
    private readonly IConnectorRepository _connectorRepository = Substitute.For<IConnectorRepository>();

    private ListConnectorsQueryHandler CreateHandler() => new(_connectorRepository);

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoConnectorsExist()
    {
        _connectorRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Connector>());

        var result = await CreateHandler().Handle(new ListConnectorsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnAllConnectors_AsDtos()
    {
        var c1 = new ConnectorBuilder().WithName("C1").WithType(ConnectorType.Slack).Build();
        var c2 = new ConnectorBuilder().WithName("C2").WithType(ConnectorType.Teams).Activated().Build();
        _connectorRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { c1, c2 });

        var result = await CreateHandler().Handle(new ListConnectorsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value.Should().Contain(x => x.Name == "C1" && x.ConnectorType == "Slack");
        result.Value.Should().Contain(x => x.Name == "C2" && x.Status == "Active");
    }
}
