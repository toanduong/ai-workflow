using FluentAssertions;
using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Domain.UnitTests.Executions;

public class WorkflowExecutionTests
{
    [Fact]
    public void Create_ShouldRaiseExecutionStartedEvent()
    {
        var execution = WorkflowExecution.Create(WorkflowId.New(), "Manual");

        execution.Status.Should().Be(ExecutionStatus.Running);
        execution.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowAI.Domain.Executions.Events.ExecutionStartedEvent>();
    }

    [Fact]
    public void Complete_ShouldChangeStatusAndRaiseEvent()
    {
        var execution = WorkflowExecution.Create(WorkflowId.New(), "API");
        execution.ClearDomainEvents();

        var result = execution.Complete("output data");

        result.IsSuccess.Should().BeTrue();
        execution.Status.Should().Be(ExecutionStatus.Completed);
        execution.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_ShouldFail_WhenNotRunning()
    {
        var execution = WorkflowExecution.Create(WorkflowId.New(), "Manual");
        execution.Complete();

        var result = execution.Complete();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Fail_ShouldChangeStatusToFailed()
    {
        var execution = WorkflowExecution.Create(WorkflowId.New(), "Manual");

        var result = execution.Fail("Something went wrong");

        result.IsSuccess.Should().BeTrue();
        execution.Status.Should().Be(ExecutionStatus.Failed);
    }

    [Fact]
    public void Cancel_ShouldChangeStatusToCancelled()
    {
        var execution = WorkflowExecution.Create(WorkflowId.New(), "Manual");

        var result = execution.Cancel();

        result.IsSuccess.Should().BeTrue();
        execution.Status.Should().Be(ExecutionStatus.Cancelled);
    }

    [Fact]
    public void AddStep_ShouldAddStepExecution()
    {
        var execution = WorkflowExecution.Create(WorkflowId.New(), "Manual");
        var stepId = Guid.NewGuid();

        var step = execution.AddStep(stepId);

        execution.Steps.Should().HaveCount(1);
        step.WorkflowStepId.Should().Be(stepId);
    }
}
