using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Application.Workflows.Queries.ListWorkflows;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.UnitTests.Workflows;

public class ListWorkflowsQueryHandlerTests
{
    private readonly IWorkflowRepository _workflowRepository = Substitute.For<IWorkflowRepository>();

    private ListWorkflowsQueryHandler CreateHandler() => new(_workflowRepository);

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoWorkflows()
    {
        _workflowRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Workflow>());

        var result = await CreateHandler().Handle(new ListWorkflowsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnDtos_WithCorrectStepCounts()
    {
        var w1 = new WorkflowBuilder().WithName("W1")
            .WithStep("A", StepType.Action).WithStep("B", StepType.Action).Build();
        var w2 = new WorkflowBuilder().WithName("W2").Build();
        _workflowRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { w1, w2 });

        var result = await CreateHandler().Handle(new ListWorkflowsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dtos = result.Value!;
        dtos.Should().HaveCount(2);
        dtos.First(x => x.Name == "W1").StepCount.Should().Be(2);
        dtos.First(x => x.Name == "W2").StepCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldMapStatusCorrectly_ForDraftAndActiveWorkflows()
    {
        var draft = new WorkflowBuilder().WithName("Draft").Build();
        var active = new WorkflowBuilder().WithName("Active").WithStep("S", StepType.Action).Activated().Build();
        _workflowRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { draft, active });

        var result = await CreateHandler().Handle(new ListWorkflowsQuery(), CancellationToken.None);

        var dtos = result.Value!;
        dtos.First(x => x.Name == "Draft").Status.Should().Be("Draft");
        dtos.First(x => x.Name == "Active").Status.Should().Be("Active");
    }
}
