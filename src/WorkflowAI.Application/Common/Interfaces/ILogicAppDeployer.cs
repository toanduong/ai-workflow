namespace WorkflowAI.Application.Common.Interfaces;

public interface ILogicAppDeployer
{
    Task<LogicAppDeployResult> DeployArmTemplateAsync(
        string armTemplateJson,
        string workflowName,
        CancellationToken cancellationToken = default);
}

public sealed record LogicAppDeployResult(
    bool Success,
    string? ResourceId,
    string? ErrorMessage = null);
