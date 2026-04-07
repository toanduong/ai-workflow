using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Application.UnitTests.Common.Fakes;
using WorkflowAI.Application.Workflows.Commands.ExecuteWorkflow;
using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.UnitTests.Workflows;

public class ExecuteWorkflowCommandHandlerTests
{
    private readonly IWorkflowRepository _workflowRepository = Substitute.For<IWorkflowRepository>();
    private readonly IExecutionRepository _executionRepository = Substitute.For<IExecutionRepository>();
    private readonly IDomainEventDispatcher _eventDispatcher = Substitute.For<IDomainEventDispatcher>();
    private readonly CurrentUserServiceFake _currentUserService = CurrentUserServiceFake.Authenticated();

    private ExecuteWorkflowCommandHandler CreateHandler() =>
        new(_workflowRepository, _executionRepository, _eventDispatcher, _currentUserService);

    [Fact]
    public async Task Handle_ShouldCreateExecution_WithAllSteps_WhenWorkflowIsActive()
    {
        var workflow = new WorkflowBuilder()
            .WithStep("Step A", StepType.AIAgent)
            .WithStep("Step B", StepType.HumanApproval)
            .Activated().Build();
        _currentUserService.DisplayName = "Alice";
        _workflowRepository.GetByIdAsync(workflow.Id, Arg.Any<CancellationToken>()).Returns(workflow);

        var result = await CreateHandler().Handle(new ExecuteWorkflowCommand(workflow.Id.Value, "{}"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(default(ExecutionId));
        await _executionRepository.Received(1).AddAsync(
            Arg.Is<WorkflowExecution>(e => e.Steps.Count == 2 && e.TriggeredBy == "Alice" && e.InputData == "{}"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUseSystemAsTriggeredBy_WhenDisplayNameIsNull()
    {
        var workflow = new WorkflowBuilder().WithStep("Step", StepType.Action).Activated().Build();
        _currentUserService.DisplayName = null;
        _workflowRepository.GetByIdAsync(workflow.Id, Arg.Any<CancellationToken>()).Returns(workflow);

        await CreateHandler().Handle(new ExecuteWorkflowCommand(workflow.Id.Value), CancellationToken.None);

        await _executionRepository.Received(1).AddAsync(
            Arg.Is<WorkflowExecution>(e => e.TriggeredBy == "System"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkflowNotFound()
    {
        _workflowRepository.GetByIdAsync(Arg.Any<WorkflowId>(), Arg.Any<CancellationToken>())
            .Returns((Workflow?)null);

        var result = await CreateHandler().Handle(new ExecuteWorkflowCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Workflow.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkflowIsNotActive()
    {
        var workflow = new WorkflowBuilder().WithStep("Step", StepType.Action).Build();
        _workflowRepository.GetByIdAsync(workflow.Id, Arg.Any<CancellationToken>()).Returns(workflow);

        var result = await CreateHandler().Handle(new ExecuteWorkflowCommand(workflow.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Workflow.NotActive");
    }

    [Fact]
    public async Task Handle_ShouldDispatchEventsAfterSave()
    {
        var workflow = new WorkflowBuilder().WithStep("Step", StepType.Action).Activated().Build();
        _workflowRepository.GetByIdAsync(workflow.Id, Arg.Any<CancellationToken>()).Returns(workflow);

        await CreateHandler().Handle(new ExecuteWorkflowCommand(workflow.Id.Value), CancellationToken.None);

        await _eventDispatcher.Received(1).DispatchEventsAsync(Arg.Any<WorkflowExecution>(), Arg.Any<CancellationToken>());
    }
}
