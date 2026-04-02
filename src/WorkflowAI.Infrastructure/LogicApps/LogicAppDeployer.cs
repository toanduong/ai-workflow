using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.LogicApps;

public sealed class LogicAppDeployer(
    ArmClient armClient,
    IOptions<AzureResourcesOptions> options,
    ILogger<LogicAppDeployer> logger) : ILogicAppDeployer
{
    public async Task<LogicAppDeployResult> DeployArmTemplateAsync(
        string armTemplateJson,
        string workflowName,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        if (!settings.DeployEnabled)
        {
            logger.LogInformation("Logic App deploy is disabled. Skipping deployment for workflow '{WorkflowName}'.", workflowName);
            return new LogicAppDeployResult(true, null);
        }

        if (string.IsNullOrEmpty(settings.SubscriptionId) || string.IsNullOrEmpty(settings.ResourceGroupName))
        {
            return new LogicAppDeployResult(false, null,
                "Azure SubscriptionId and ResourceGroupName must be configured in AzureResources settings.");
        }

        try
        {
            var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(
                settings.SubscriptionId,
                settings.ResourceGroupName);

            var resourceGroup = armClient.GetResourceGroupResource(resourceGroupId);

            var safeName = System.Text.RegularExpressions.Regex.Replace(workflowName, @"[^a-zA-Z0-9\-_.]", "-");
            var deploymentName = $"workflow-{safeName}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var deploymentContent = new ArmDeploymentContent(new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
            {
                Template = BinaryData.FromString(armTemplateJson)
            });

            logger.LogInformation("Deploying Logic App ARM template for workflow '{WorkflowName}', deployment '{DeploymentName}'.",
                workflowName, deploymentName);

            var operation = await resourceGroup.GetArmDeployments()
                .CreateOrUpdateAsync(WaitUntil.Completed, deploymentName, deploymentContent, cancellationToken);

            var resourceId = $"/subscriptions/{settings.SubscriptionId}"
                + $"/resourceGroups/{settings.ResourceGroupName}"
                + $"/providers/Microsoft.Logic/workflows/{safeName}";

            logger.LogInformation("Logic App deployed successfully. ResourceId: '{ResourceId}'.", resourceId);

            return new LogicAppDeployResult(true, resourceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deploy Logic App ARM template for workflow '{WorkflowName}'.", workflowName);
            return new LogicAppDeployResult(false, null, ex.Message);
        }
    }
}
