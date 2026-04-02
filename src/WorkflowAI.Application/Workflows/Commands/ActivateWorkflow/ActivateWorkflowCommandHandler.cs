using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.Workflows.Commands.ActivateWorkflow;

public sealed class ActivateWorkflowCommandHandler(
    IWorkflowRepository workflowRepository,
    ILogicAppScriptGenerator scriptGenerator,
    ILogicAppDeployer logicAppDeployer,
    IBlobStorageService blobStorageService)
    : IRequestHandler<ActivateWorkflowCommand, Result<ActivateWorkflowResult>>
{
    private const string ArmTemplatesContainer = "arm-templates";

    public async Task<Result<ActivateWorkflowResult>> Handle(
        ActivateWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workflow = await workflowRepository.GetByIdAsync(
            WorkflowId.From(request.WorkflowId), cancellationToken);

        if (workflow is null)
            return Error.NotFound("Workflow.NotFound", $"Workflow {request.WorkflowId} not found.");

        var activationResult = workflow.Activate();
        if (!activationResult.IsSuccess)
            return activationResult.Error!;

        var generationResult = await scriptGenerator.GenerateArmTemplateAsync(workflow, cancellationToken);
        if (!generationResult.Success)
            return Error.Unexpected("Workflow.GenerationFailed", generationResult.ErrorMessage ?? "Failed to generate Logic App template.");

        await blobStorageService.UploadAsync(
            ArmTemplatesContainer,
            generationResult.FileName,
            generationResult.Content,
            cancellationToken);

        var deployResult = await logicAppDeployer.DeployArmTemplateAsync(
            generationResult.Content, workflow.Name, cancellationToken);

        if (!deployResult.Success)
            return Error.Unexpected("Workflow.DeployFailed", deployResult.ErrorMessage ?? "Failed to deploy Logic App.");

        if (deployResult.ResourceId is not null)
            workflow.SetLogicAppResourceId(deployResult.ResourceId);

        await workflowRepository.UpdateAsync(workflow, cancellationToken);

        return new ActivateWorkflowResult(generationResult.Content, generationResult.FileName, deployResult.ResourceId);
    }
}
