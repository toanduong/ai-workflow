using System.Text.Json.Nodes;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Connectors;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Infrastructure.LogicApps;

public sealed class LogicAppScriptGenerator(
    IConnectorRepository connectorRepository,
    StepToConnectorMapper stepMapper) : ILogicAppScriptGenerator
{
    public async Task<LogicAppGenerationResult> GenerateArmTemplateAsync(Workflow workflow, CancellationToken ct = default)
    {
        var content = await GenerateArmJsonAsync(workflow, ct);
        var fileName = $"{workflow.Name.Replace(" ", "-")}-logic-app.json";
        return new LogicAppGenerationResult(content, fileName, true);
    }

    public Task<LogicAppGenerationResult> GenerateBicepTemplateAsync(Workflow workflow, CancellationToken ct = default)
    {
        // Bicep generation is a future enhancement
        var fileName = $"{workflow.Name.Replace(" ", "-")}-logic-app.bicep";
        return Task.FromResult(new LogicAppGenerationResult("// Bicep template generation not yet implemented", fileName, false, "Bicep generation not yet supported. Use ARM template."));
    }

    private async Task<string> GenerateArmJsonAsync(Workflow workflow, CancellationToken ct)
    {
        var builder = new ArmTemplateBuilder();
        var actions = new JsonObject();
        var connections = new JsonObject();
        var hasConnections = false;
        string? previousActionName = null;

        foreach (var step in workflow.Steps.OrderBy(s => s.OrderIndex))
        {
            var actionType = stepMapper.MapToLogicAppActionType(step.StepType, step.Configuration);
            var actionName = step.Name.Replace(" ", "_");

            if (step.ConnectorId.HasValue)
            {
                var connector = await connectorRepository.GetByIdAsync(step.ConnectorId.Value, ct);
                if (connector is not null && connector.AzureApiConnectionId is not null)
                {
                    builder.AddApiConnection(connector);
                    var connName = $"{connector.ConnectorType.Name.ToLowerInvariant()}-connection";
                    connections[connector.ConnectorType.Name.ToLowerInvariant()] = new JsonObject
                    {
                        ["connectionId"] = $"[resourceId('Microsoft.Web/connections', '{connName}')]",
                        ["connectionName"] = connName,
                        ["id"] = $"[subscriptionResourceId('Microsoft.Web/locations/managedApis', resourceGroup().location, '{connector.ConnectorType.Name.ToLowerInvariant()}')]"
                    };
                    hasConnections = true;
                }
            }

            var runAfter = previousActionName is null
                ? new JsonObject()
                : new JsonObject { [previousActionName] = new JsonArray { (JsonNode)"Succeeded" } };

            JsonObject stepAction;
            if (step.StepType.Name == nameof(StepType.HumanApproval) && string.IsNullOrWhiteSpace(step.Configuration))
            {
                // HumanApproval without URL: emit a Compose step as a placeholder annotation
                stepAction = new JsonObject
                {
                    ["type"] = "Compose",
                    ["runAfter"] = runAfter,
                    ["inputs"] = $"Waiting for human approval: {step.Name}"
                };
            }
            else
            {
                stepAction = new JsonObject
                {
                    ["type"] = actionType,
                    ["runAfter"] = runAfter,
                    ["inputs"] = new JsonObject
                    {
                        ["method"] = step.HttpMethod ?? stepMapper.MapToHttpMethod(step.StepType),
                        ["uri"] = step.Configuration ?? ""
                    }
                };
            }

            actions[actionName] = stepAction;
            previousActionName = actionName;
        }

        builder.AddLogicAppWorkflow(workflow.Name.Replace(" ", "-"), actions, hasConnections ? connections : null);
        return builder.Build();
    }
}
