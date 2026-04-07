using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Application.Workflows.Queries.GetWorkflow;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.UnitTests.Workflows;

public class GetWorkflowQueryHandlerTests
{
    private readonly IWorkflowRepository _workflowRepository = Substitute.For<IWorkflowRepository>();

    private GetWorkflowQueryHandler CreateHandler() => new(_workflowRepository);

    [Fact]
    public async Task Handle_ShouldReturnWorkflowDto_WithCorrectStepMapping()
    {
        var workflow = new WorkflowBuilder()
            .WithName("My Workflow")
            .WithStep("Step 1", StepType.AIAgent)
            .Activated().Build();
        _workflowRepository.GetByIdAsync(workflow.Id, Arg.Any<CancellationToken>()).Returns(workflow);

        var result = await CreateHandler().Handle(new GetWorkflowQuery(workflow.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("My Workflow");
        result.Value.Status.Should().Be("Active");
        result.Value.Steps.Should().HaveCount(1);
        result.Value.Steps[0].StepType.Should().Be("AIAgent");
        result.Value.Steps[0].OrderIndex.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReturnNullLogicAppResourceId_WhenNotSet()
    {
        var workflow = new WorkflowBuilder().WithStep("Step", StepType.Action).Build();
        _workflowRepository.GetByIdAsync(workflow.Id, Arg.Any<CancellationToken>()).Returns(workflow);

        var result = await CreateHandler().Handle(new GetWorkflowQuery(workflow.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.LogicAppResourceId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkflowNotFound()
    {
        _workflowRepository.GetByIdAsync(Arg.Any<WorkflowId>(), Arg.Any<CancellationToken>())
            .Returns((Workflow?)null);

        var result = await CreateHandler().Handle(new GetWorkflowQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Workflow.NotFound");
    }
}
