using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Workflows.Commands.GenerateArmTemplate;

public sealed record GenerateArmTemplateFromPromptCommand(
    Guid TenantId,
    string Prompt,
    bool Deploy = false,
    DeploymentConfig? DeploymentConfig = null) : IRequest<Result<GenerateArmTemplateFromPromptResult>>;

public sealed record DeploymentConfig(
    string SubscriptionId,
    string ResourceGroupName,
    string Location);

public sealed record GenerateArmTemplateFromPromptResult(
    string WorkflowName,
    string ArmTemplateJson,
    IReadOnlyList<string> ConnectorsUsed,
    IReadOnlyList<string> MissingApis,
    DeploymentInfo? Deployment = null);

public sealed record DeploymentInfo(
    string Status,
    string? DeploymentId,
    string? LogicAppResourceId,
    string? LogicAppUrl,
    DateTime? DeployedAt,
    string? ErrorMessage);
