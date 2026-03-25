using FluentAssertions;
using WorkflowAI.Domain.Users;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Domain.UnitTests.Workflows;

public class WorkflowTests
{
    [Fact]
    public void Create_ShouldReturnWorkflow_WithDraftStatus()
    {
        var userId = UserId.New();
        var workflow = Workflow.Create("Test Workflow", "Description", userId);

        workflow.Name.Should().Be("Test Workflow");
        workflow.Description.Should().Be("Description");
        workflow.Status.Should().Be(WorkflowStatus.Draft);
        workflow.CreatedByUserId.Should().Be(userId);
        workflow.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void AddStep_ShouldSucceed_WhenWorkflowIsDraft()
    {
        var workflow = Workflow.Create("Test", null, UserId.New());

        var result = workflow.AddStep("Step 1", StepType.AIAgent);

        result.IsSuccess.Should().BeTrue();
        workflow.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void Activate_ShouldFail_WhenNoSteps()
    {
        var workflow = Workflow.Create("Test", null, UserId.New());

        var result = workflow.Activate();

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Workflow.NoSteps");
    }

    [Fact]
    public void Activate_ShouldSucceed_WhenHasSteps()
    {
        var workflow = Workflow.Create("Test", null, UserId.New());
        workflow.AddStep("Step 1", StepType.AIAgent);

        var result = workflow.Activate();

        result.IsSuccess.Should().BeTrue();
        workflow.Status.Should().Be(WorkflowStatus.Active);
    }
}
