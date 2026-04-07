using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.Executions.Commands.AdvanceStep;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.UnitTests.Executions;

public class AdvanceStepCommandHandlerTests
{
    private readonly IWorkflowRepository _workflowRepository = Substitute.For<IWorkflowRepository>();
    private readonly IExecutionRepository _executionRepository = Substitute.For<IExecutionRepository>();
    private readonly IDomainEventDispatcher _eventDispatcher = Substitute.For<IDomainEventDispatcher>();

    private AdvanceStepCommandHandler CreateHandler() =>
        new(_workflowRepository, _executionRepository, _eventDispatcher);

    [Fact]
    public async Task Handle_ShouldAddNextStep_WhenCurrentIsNotLastStep()
    {
        var workflow = new WorkflowBuilder()
            .WithStep("Step A", StepType.AIAgent)
            .WithStep("Step B", StepType.Action)
            .Activated().Build();
        var steps = workflow.Steps.OrderBy(s => s.OrderIndex).ToList();
        var firstStepId = steps[0].Id;
        var secondStepId = steps[1].Id;

        var execution = new WorkflowExecutionBuilder().WithWorkflowId(workflow.Id).Build();
        _executionRepository.GetByIdAsync(execution.Id, Arg.Any<CancellationToken>()).Returns(execution);
        _workflowRepository.GetByIdAsync(workflow.Id, Arg.Any<CancellationToken>()).Returns(workflow);

        var result = await CreateHandler().Handle(
            new AdvanceStepCommand(execution.Id.Value, firstStepId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _executionRepository.Received(1).UpdateAsync(
            Arg.Is<WorkflowExecution>(e => e.Steps.Any(s => s.WorkflowStepId == secondStepId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCompleteExecution_WhenCurrentIsLastStep()
    {
        var workflow = new WorkflowBuilder()
            .WithStep("Only Step", StepType.Action)
            .Activated().Build();
        var onlyStepId = workflow.Steps.Single().Id;

        var execution = new WorkflowExecutionBuilder().WithWorkflowId(workflow.Id).Build();
        _executionRepository.GetByIdAsync(execution.Id, Arg.Any<CancellationToken>()).Returns(execution);
        _workflowRepository.GetByIdAsync(workflow.Id, Arg.Any<CancellationToken>()).Returns(workflow);

        var result = await CreateHandler().Handle(
            new AdvanceStepCommand(execution.Id.Value, onlyStepId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _executionRepository.Received(1).UpdateAsync(
            Arg.Is<WorkflowExecution>(e => e.Status == ExecutionStatus.Completed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenExecutionNotFound()
    {
        _executionRepository.GetByIdAsync(Arg.Any<ExecutionId>(), Arg.Any<CancellationToken>())
            .Returns((WorkflowExecution?)null);

        var result = await CreateHandler().Handle(
            new AdvanceStepCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Execution.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkflowNotFound()
    {
        var execution = new WorkflowExecutionBuilder().Build();
        _executionRepository.GetByIdAsync(execution.Id, Arg.Any<CancellationToken>()).Returns(execution);
        _workflowRepository.GetByIdAsync(Arg.Any<WorkflowId>(), Arg.Any<CancellationToken>())
            .Returns((Workflow?)null);

        var result = await CreateHandler().Handle(
            new AdvanceStepCommand(execution.Id.Value, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Workflow.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenCompletedStepIdNotInWorkflow()
    {
        var workflow = new WorkflowBuilder().WithStep("Step", StepType.Action).Activated().Build();
        var execution = new WorkflowExecutionBuilder().WithWorkflowId(workflow.Id).Build();
        _executionRepository.GetByIdAsync(execution.Id, Arg.Any<CancellationToken>()).Returns(execution);
        _workflowRepository.GetByIdAsync(workflow.Id, Arg.Any<CancellationToken>()).Returns(workflow);

        var result = await CreateHandler().Handle(
            new AdvanceStepCommand(execution.Id.Value, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Step.NotFound");
    }
}
