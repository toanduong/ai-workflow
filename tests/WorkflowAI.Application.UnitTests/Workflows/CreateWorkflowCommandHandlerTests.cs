using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.Workflows.Commands.CreateWorkflow;
using WorkflowAI.Domain.Users;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.UnitTests.Workflows;

public class CreateWorkflowCommandHandlerTests
{
    private readonly IWorkflowRepository _workflowRepository = Substitute.For<IWorkflowRepository>();
    private readonly IDomainEventDispatcher _eventDispatcher = Substitute.For<IDomainEventDispatcher>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();

    private CreateWorkflowCommandHandler CreateHandler() =>
        new(_workflowRepository, _eventDispatcher, _currentUserService);

    [Fact]
    public async Task Handle_ShouldCreateWorkflow_WhenValid()
    {
        _currentUserService.UserId.Returns(UserId.New());
        var handler = CreateHandler();
        var templateId = Guid.NewGuid();
        var steps = new List<CreateWorkflowStepDto>
{
    new CreateWorkflowStepDto(
        Name: "Send Welcome Email",
        StepType: "Notification",
        Configuration: "https://api.internal/notifications/welcome",
        RequiredRole: null,
        TimeoutMinutes: 30,
        OnTimeoutAction: null),

    new CreateWorkflowStepDto(
        Name: "AI KYC Verification",
        StepType: "AIAgent",
        Configuration: "https://api.internal/agents/kyc-verify",
        RequiredRole: null,
        TimeoutMinutes: 60,
        OnTimeoutAction: "Skip"),

    new CreateWorkflowStepDto(
        Name: "Manager Approval",
        StepType: "HumanApproval",
        Configuration: null,
        RequiredRole: "Manager",
        TimeoutMinutes: 1440,
        OnTimeoutAction: "Escalate"),

    new CreateWorkflowStepDto(
        Name: "Provision Account",
        StepType: "Action",
        Configuration: "https://api.internal/accounts/provision",
        RequiredRole: null,
        TimeoutMinutes: 15,
        OnTimeoutAction: "Retry")
};
        var command = new CreateWorkflowCommand("Test Workflow", "Description", templateId, steps);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().NotBeEmpty();
        await _workflowRepository.Received(1).AddAsync(Arg.Any<Workflow>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNotAuthenticated()
    {
        _currentUserService.UserId.Returns((UserId?)null);
        var handler = CreateHandler();
        var command = new CreateWorkflowCommand("Test", null, null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Auth.Required");
    }

    [Fact]
    public async Task Handle_ShouldCreateWithSteps_WhenStepsProvided()
    {
        _currentUserService.UserId.Returns(UserId.New());
        var handler = CreateHandler();
        var steps = new List<CreateWorkflowStepDto>
        {
            new("Step 1", "AIAgent", null, null, null, 60, null),
            new("Step 2", "HumanApproval", null, null, "Admin", 120, "Escalate")
        };
        var command = new CreateWorkflowCommand("Workflow", null, null, steps);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _workflowRepository.Received(1).AddAsync(
            Arg.Is<Workflow>(w => w.Steps.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenInvalidStepType()
    {
        _currentUserService.UserId.Returns(UserId.New());
        var handler = CreateHandler();
        var steps = new List<CreateWorkflowStepDto>
        {
            new("Step 1", "InvalidType", null, null, null, 60, null)
        };
        var command = new CreateWorkflowCommand("Workflow", null, null, steps);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Step.InvalidType");
    }
}
