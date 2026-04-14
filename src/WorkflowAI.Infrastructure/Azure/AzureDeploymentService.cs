using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Azure;

/// <summary>
/// Service for deploying ARM templates to Azure using Azure Resource Manager SDK.
/// </summary>
public sealed class AzureDeploymentService : IAzureDeploymentService
{
    private readonly ArmClient _armClient;
    private readonly ILogger<AzureDeploymentService> _logger;

    public AzureDeploymentService(
        TokenCredential credential,
        ILogger<AzureDeploymentService> logger)
    {
        _armClient = new ArmClient(credential);
        _logger = logger;
    }

    public async Task<DeploymentResult> DeployArmTemplateAsync(
        string subscriptionId,
        string resourceGroupName,
        string deploymentName,
        string armTemplateJson,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Starting ARM template deployment: {DeploymentName} to {ResourceGroup}",
                deploymentName,
                resourceGroupName);

            // Get subscription and resource group
            var subscriptionResource = _armClient.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

            var resourceGroupResource = await subscriptionResource
                .GetResourceGroups()
                .GetAsync(resourceGroupName, cancellationToken);

            // Convert parameters to Azure format
            var armParameters = ConvertParametersToArmFormat(parameters);

            // Create deployment content
            var deploymentProperties = new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
            {
                Template = BinaryData.FromString(armTemplateJson),
                Parameters = BinaryData.FromObjectAsJson(armParameters)
            };

            var deployment = new ArmDeploymentContent(deploymentProperties);

            // Execute deployment (synchronous for now)
            var operation = await resourceGroupResource.Value
                .GetArmDeployments()
                .CreateOrUpdateAsync(
                    WaitUntil.Completed,
                    deploymentName,
                    deployment,
                    cancellationToken);

            var deploymentResource = operation.Value;
            var deploymentData = deploymentResource.Data;

            // Extract Logic App resource ID from deployment outputs
            string? logicAppResourceId = ExtractLogicAppResourceId(deploymentData);
            string? logicAppUrl = logicAppResourceId != null
                ? BuildLogicAppPortalUrl(logicAppResourceId)
                : null;

            var success = deploymentData.Properties.ProvisioningState == ResourcesProvisioningState.Succeeded;

            _logger.LogInformation(
                "Deployment {DeploymentName} completed with status: {Status}",
                deploymentName,
                deploymentData.Properties.ProvisioningState);

            return new DeploymentResult(
                Success: success,
                DeploymentId: deploymentData.Id!,
                Status: deploymentData.Properties.ProvisioningState?.ToString() ?? "Unknown",
                LogicAppResourceId: logicAppResourceId,
                LogicAppUrl: logicAppUrl,
                ErrorMessage: success ? null : ExtractErrorMessage(deploymentData));
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex,
                "Azure deployment failed: {DeploymentName} - {ErrorCode}: {Message}",
                deploymentName,
                ex.ErrorCode,
                ex.Message);

            return new DeploymentResult(
                Success: false,
                DeploymentId: string.Empty,
                Status: "Failed",
                LogicAppResourceId: null,
                LogicAppUrl: null,
                ErrorMessage: $"{ex.ErrorCode}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during deployment: {DeploymentName}",
                deploymentName);

            return new DeploymentResult(
                Success: false,
                DeploymentId: string.Empty,
                Status: "Failed",
                LogicAppResourceId: null,
                LogicAppUrl: null,
                ErrorMessage: ex.Message);
        }
    }

    public async Task<DeploymentStatus> GetDeploymentStatusAsync(
        string subscriptionId,
        string resourceGroupName,
        string deploymentName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriptionResource = _armClient.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

            var resourceGroupResource = await subscriptionResource
                .GetResourceGroups()
                .GetAsync(resourceGroupName, cancellationToken);

            var deployment = await resourceGroupResource.Value
                .GetArmDeployments()
                .GetAsync(deploymentName, cancellationToken);

            var deploymentData = deployment.Value.Data;
            var status = deploymentData.Properties.ProvisioningState?.ToString() ?? "Unknown";

            string? logicAppResourceId = ExtractLogicAppResourceId(deploymentData);
            string? logicAppUrl = logicAppResourceId != null
                ? BuildLogicAppPortalUrl(logicAppResourceId)
                : null;

            return new DeploymentStatus(
                Status: status,
                LogicAppResourceId: logicAppResourceId,
                LogicAppUrl: logicAppUrl,
                CompletedAt: deploymentData.Properties.Timestamp?.UtcDateTime,
                ErrorMessage: status == "Failed" ? ExtractErrorMessage(deploymentData) : null);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new DeploymentStatus(
                Status: "NotFound",
                LogicAppResourceId: null,
                LogicAppUrl: null,
                CompletedAt: null,
                ErrorMessage: "Deployment not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deployment status: {DeploymentName}", deploymentName);
            throw;
        }
    }

    private static Dictionary<string, object> ConvertParametersToArmFormat(
        Dictionary<string, string> parameters)
    {
        return parameters.ToDictionary(
            kvp => kvp.Key,
            kvp => (object)new { value = kvp.Value });
    }

    private static string? ExtractLogicAppResourceId(ArmDeploymentData deploymentData)
    {
        try
        {
            if (deploymentData.Properties.OutputResources == null ||
                deploymentData.Properties.OutputResources.Count == 0)
            {
                return null;
            }

            // Find the Logic App resource (type: Microsoft.Logic/workflows)
            var logicAppResource = deploymentData.Properties.OutputResources
                .FirstOrDefault(r => r.Id?.ToString().Contains("/Microsoft.Logic/workflows/") == true);

            return logicAppResource?.Id?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string BuildLogicAppPortalUrl(string resourceId)
    {
        // Build Azure Portal URL for the Logic App
        return $"https://portal.azure.com/#resource{resourceId}";
    }

    private static string? ExtractErrorMessage(ArmDeploymentData deploymentData)
    {
        try
        {
            if (deploymentData.Properties.Error == null)
            {
                return null;
            }

            var error = deploymentData.Properties.Error;
            return $"{error.Code}: {error.Message}";
        }
        catch
        {
            return "Unknown error occurred during deployment";
        }
    }
}
