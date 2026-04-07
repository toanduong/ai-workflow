using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Application.Workflows.Commands.ActivateWorkflow;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.UnitTests.Workflows;

public class ActivateWorkflowCommandHandlerTests
{
    private readonly IWorkflowRepository _workflowRepository = Substitute.For<IWorkflowRepository>();
    private readonly ILogicAppScriptGenerator _scriptGenerator = Substitute.For<ILogicAppScriptGenerator>();
    private readonly ILogicAppDeployer _logicAppDeployer = Substitute.For<ILogicAppDeployer>();
    private readonly IBlobStorageService _blobStorageService = Substitute.For<IBlobStorageService>();

    private ActivateWorkflowCommandHandler CreateHandler() =>
        new(_workflowRepository, _scriptGenerator, _logicAppDeployer, _blobStorageService);

    [Fact]
    public async Task Handle_ShouldReturnArmTemplate_WhenWorkflowIsActivated()
    {
        var workflow = new WorkflowBuilder().WithStep("Step 1", StepType.AIAgent).Build();
        _workflowRepository.GetByIdAsync(workflow.Id, Arg.Any<CancellationToken>()).Returns(workflow);
        _scriptGenerator.GenerateArmTemplateAsync(workflow, Arg.Any<CancellationToken>())
            .Returns(new LogicAppGenerationResult("{}", "template.json", true));
        _logicAppDeployer.DeployArmTemplateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LogicAppDeployResult(true, "/subscriptions/123/resourceGroups/rg/providers/Microsoft.Logic/workflows/My-Logic-App"));

        var result = await CreateHandler().Handle(new ActivateWorkflowCommand(workflow.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ArmTemplateContent.Should().Be("{}");
        result.Value.FileName.Should().Be("template.json");
        await _workflowRepository.Received(1).UpdateAsync(
            Arg.Is<Workflow>(w => w.Status == WorkflowStatus.Active), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkflowNotFound()
    {
        _workflowRepository.GetByIdAsync(Arg.Any<WorkflowId>(), Arg.Any<CancellationToken>())
            .Returns((Workflow?)null);

        var result = await CreateHandler().Handle(new ActivateWorkflowCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Workflow.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkflowHasNoSteps()
    {
        var workflow = new WorkflowBuilder().Build();
        _workflowRepository.GetByIdAsync(workflow.Id, Arg.Any<CancellationToken>()).Returns(workflow);

        var result = await CreateHandler().Handle(new ActivateWorkflowCommand(workflow.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Workflow.NoSteps");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkflowAlreadyActive()
    {
        var workflow = new WorkflowBuilder().WithStep("Step 1", StepType.Action).Activated().Build();
        _workflowRepository.GetByIdAsync(workflow.Id, Arg.Any<CancellationToken>()).Returns(workflow);

        var result = await CreateHandler().Handle(new ActivateWorkflowCommand(workflow.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Workflow.NotDraft");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTemplateGenerationFails()
    {
        var workflow = new WorkflowBuilder().WithStep("Step 1", StepType.AIAgent).Build();
        _workflowRepository.GetByIdAsync(workflow.Id, Arg.Any<CancellationToken>()).Returns(workflow);
        _scriptGenerator.GenerateArmTemplateAsync(workflow, Arg.Any<CancellationToken>())
            .Returns(new LogicAppGenerationResult("", "", false, "Template engine error"));

        var result = await CreateHandler().Handle(new ActivateWorkflowCommand(workflow.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Workflow.GenerationFailed");
        await _workflowRepository.DidNotReceive().UpdateAsync(Arg.Any<Workflow>(), Arg.Any<CancellationToken>());
    }
}
