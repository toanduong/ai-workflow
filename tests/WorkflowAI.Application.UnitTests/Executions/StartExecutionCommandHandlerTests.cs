using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.Executions.Commands.StartExecution;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.UnitTests.Executions;

public class StartExecutionCommandHandlerTests
{
    private readonly IWorkflowRepository _workflowRepository = Substitute.For<IWorkflowRepository>();
    private readonly IExecutionRepository _executionRepository = Substitute.For<IExecutionRepository>();

    private StartExecutionCommandHandler CreateHandler() =>
        new(_workflowRepository, _executionRepository);

    [Fact]
    public async Task Handle_ShouldAddFirstStep_WhenWorkflowHasSteps()
    {
        var workflow = new WorkflowBuilder()
            .WithStep("S1", StepType.AIAgent)
            .WithStep("S2", StepType.Action)
            .Activated().Build();
        var firstStepId = workflow.Steps.OrderBy(s => s.OrderIndex).First().Id;
        var execution = new WorkflowExecutionBuilder().WithWorkflowId(workflow.Id).Build();

        _workflowRepository.GetByIdAsync(WorkflowId.From(workflow.Id.Value), Arg.Any<CancellationToken>()).Returns(workflow);
        _executionRepository.GetByIdAsync(execution.Id, Arg.Any<CancellationToken>()).Returns(execution);

        var result = await CreateHandler().Handle(
            new StartExecutionCommand(workflow.Id.Value, execution.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _executionRepository.Received(1).UpdateAsync(
            Arg.Is<WorkflowExecution>(e => e.Steps.Count == 1 && e.Steps[0].WorkflowStepId == firstStepId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceedWithoutUpdate_WhenWorkflowHasNoSteps()
    {
        var workflow = new WorkflowBuilder().Build();
        var execution = new WorkflowExecutionBuilder().WithWorkflowId(workflow.Id).Build();

        _workflowRepository.GetByIdAsync(WorkflowId.From(workflow.Id.Value), Arg.Any<CancellationToken>()).Returns(workflow);
        _executionRepository.GetByIdAsync(execution.Id, Arg.Any<CancellationToken>()).Returns(execution);

        var result = await CreateHandler().Handle(
            new StartExecutionCommand(workflow.Id.Value, execution.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _executionRepository.DidNotReceive().UpdateAsync(Arg.Any<WorkflowExecution>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkflowNotFound()
    {
        _workflowRepository.GetByIdAsync(Arg.Any<WorkflowId>(), Arg.Any<CancellationToken>())
            .Returns((Workflow?)null);

        var result = await CreateHandler().Handle(
            new StartExecutionCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Workflow.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenExecutionNotFound()
    {
        var workflow = new WorkflowBuilder().Build();
        _workflowRepository.GetByIdAsync(WorkflowId.From(workflow.Id.Value), Arg.Any<CancellationToken>()).Returns(workflow);
        _executionRepository.GetByIdAsync(Arg.Any<ExecutionId>(), Arg.Any<CancellationToken>())
            .Returns((WorkflowExecution?)null);

        var result = await CreateHandler().Handle(
            new StartExecutionCommand(workflow.Id.Value, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Execution.NotFound");
    }
}
