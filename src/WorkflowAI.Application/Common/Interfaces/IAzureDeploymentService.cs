namespace WorkflowAI.Application.Common.Interfaces;

/// <summary>
/// Service for deploying ARM templates to Azure Resource Manager.
/// </summary>
public interface IAzureDeploymentService
{
    /// <summary>
    /// Deploy an ARM template to Azure Logic Apps.
    /// </summary>
    /// <param name="subscriptionId">Azure subscription ID</param>
    /// <param name="resourceGroupName">Target resource group name</param>
    /// <param name="deploymentName">Unique deployment name (typically the workflow name)</param>
    /// <param name="armTemplateJson">ARM template JSON string</param>
    /// <param name="parameters">Parameter values for the template</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deployment result with status and URLs</returns>
    Task<DeploymentResult> DeployArmTemplateAsync(
        string subscriptionId,
        string resourceGroupName,
        string deploymentName,
        string armTemplateJson,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the status of an existing deployment.
    /// </summary>
    /// <param name="subscriptionId">Azure subscription ID</param>
    /// <param name="resourceGroupName">Resource group name</param>
    /// <param name="deploymentName">Deployment name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current deployment status</returns>
    Task<DeploymentStatus> GetDeploymentStatusAsync(
        string subscriptionId,
        string resourceGroupName,
        string deploymentName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an ARM template deployment operation.
/// </summary>
public sealed record DeploymentResult(
    bool Success,
    string DeploymentId,
    string Status,
    string? LogicAppResourceId,
    string? LogicAppUrl,
    string? ErrorMessage);

/// <summary>
/// Status of a deployment.
/// </summary>
public sealed record DeploymentStatus(
    string Status,
    string? LogicAppResourceId,
    string? LogicAppUrl,
    DateTime? CompletedAt,
    string? ErrorMessage);
