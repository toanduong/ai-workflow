using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Templates;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.GenerateConnectorAssets;

public sealed class GenerateConnectorAssetsCommandHandler(
    ITenantConnectorRepository repository,
    ITemplateRepository templateRepository,
    IAnthropicService claudeAIService,
    ICurrentUserService currentUserService,
    ILogger<GenerateConnectorAssetsCommandHandler> logger)
    : IRequestHandler<GenerateConnectorAssetsCommand, Result<GenerateConnectorAssetsResult>>
{
    public async Task<Result<GenerateConnectorAssetsResult>> Handle(
        GenerateConnectorAssetsCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated)
            return Error.Unauthorized("Auth.Required", "User must be authenticated.");

        var id = TenantConnectorId.From(request.TenantConnectorId);
        var connector = await repository.GetByIdAsync(id, cancellationToken);

        if (connector is null)
            return Error.NotFound("TenantConnector.NotFound",
                $"TenantConnector {request.TenantConnectorId} not found.");

        if (connector.Status != TenantConnectorStatus.Active)
            return Error.Validation("TenantConnector.NotActive",
                $"Connector must be Active before generating assets. Current status: {connector.Status.Name}");

        // Idempotency check — skip if API operations already exist for this connector
        var existingApis = await repository.GetApisByConnectorAsync(connector.Id, cancellationToken);
        if (existingApis.Count > 0)
        {
            logger.LogInformation(
                "API operations already exist for connector {ConnectorId}, skipping generation",
                request.TenantConnectorId);
            return new GenerateConnectorAssetsResult(
                JsonSerializer.Serialize(existingApis.Select(a => a.Metadata).ToList()),
                "[]");
        }

        logger.LogInformation("Generating connector assets for {ConnectorId} ({ConnectorName})",
            request.TenantConnectorId, connector.ConnectorName);

        var tools = new[]
        {
            new AIToolDefinition(
                "create_api_operation",
                "Define an API operation (method + path) supported by the connector",
                """{"type":"object","properties":{"method":{"type":"string"},"path":{"type":"string"},"description":{"type":"string"},"requestBody":{"type":"object"},"responseSchema":{"type":"object"}},"required":["method","path","description"]}"""),
            new AIToolDefinition(
                "create_workflow_template",
                "Define a reusable workflow template for the connector",
                """{"type":"object","properties":{"name":{"type":"string"},"trigger":{"type":"string"},"steps":{"type":"array","items":{"type":"object"}},"description":{"type":"string"}},"required":["name","trigger","steps","description"]}""")
        };

        var prompt = $"""
            You are an API and workflow generator for a workflow automation platform.
            Based on the following connector metadata and info for "{connector.ConnectorName}",
            generate API operations and workflow templates.

            METADATA:
            {connector.Metadata}

            INFO:
            {connector.Info}

            Use the create_api_operation tool for each API operation the connector supports.
            Use the create_workflow_template tool for each reusable workflow template.
            Generate at least 3 API operations and 2 workflow templates.
            """;

        var aiResult = await claudeAIService.CompleteWithToolsAsync(prompt, tools, cancellationToken: cancellationToken);

        if (!aiResult.Success)
        {
            logger.LogError("Claude failed to generate assets for connector {ConnectorId}: {Error}",
                request.TenantConnectorId, aiResult.ErrorMessage);
            return Error.Unexpected("TenantConnector.AssetGenerationFailed",
                aiResult.ErrorMessage ?? "Claude failed to generate connector assets.");
        }

        var toolCalls = aiResult.ToolCalls ?? [];

        // API operations → TenantConnectorApis (SRP: health checks + workflow step building)
        var apiOperationJsons = toolCalls
            .Where(t => t.ToolName == "create_api_operation")
            .Select(t => t.InputJson)
            .ToList();

        var apiList = BuildApiList(connector, apiOperationJsons);
        if (apiList.Count > 0)
            await repository.AddApisAsync(apiList, cancellationToken);

        // Workflow templates → WorkflowTemplates (SRP: user-instantiable workflows)
        var workflowJsons = toolCalls
            .Where(t => t.ToolName == "create_workflow_template")
            .Select(t => t.InputJson)
            .ToList();

        foreach (var wfJson in workflowJsons)
        {
            try
            {
                var wfDoc = JsonDocument.Parse(wfJson).RootElement;
                var name = wfDoc.TryGetProperty("name", out var n) ? n.GetString() : "Unnamed Workflow";
                var description = wfDoc.TryGetProperty("description", out var d) ? d.GetString() : null;
                var stepsElement = wfDoc.TryGetProperty("steps", out var s) ? s.GetRawText() : "[]";

                var template = WorkflowTemplate.Create(
                    name: $"{connector.ConnectorName}: {name}",
                    description: description,
                    category: $"{connector.ConnectorName}/Workflow",
                    defaultSteps: stepsElement);

                await templateRepository.AddAsync(template, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping malformed workflow template JSON for connector {ConnectorId}",
                    request.TenantConnectorId);
            }
        }

        return new GenerateConnectorAssetsResult(
            JsonSerializer.Serialize(apiOperationJsons),
            JsonSerializer.Serialize(workflowJsons));
    }

    private List<TenantConnectorApi> BuildApiList(TenantConnector connector, List<string> operationJsons)
    {
        var baseUrl = ExtractBaseUrl(connector.Metadata);
        List<TenantConnectorApi> apis = [];

        foreach (var opJson in operationJsons)
        {
            try
            {
                var doc = JsonDocument.Parse(opJson).RootElement;
                var method = doc.TryGetProperty("method", out var m) ? m.GetString() : "GET";
                var path = doc.TryGetProperty("path", out var p) ? p.GetString() : string.Empty;

                var urlTemplate = !string.IsNullOrEmpty(path) &&
                                  path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? path
                    : $"{baseUrl}{path}";

                apis.Add(TenantConnectorApi.Create(
                    connector.Id,
                    connector.TenantId,
                    connector.ConnectorName,
                    apiName: $"{method} {path}",
                    httpMethod: method ?? "GET",
                    urlTemplate: urlTemplate,
                    metadata: opJson));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping malformed API operation JSON for connector {ConnectorId}",
                    connector.Id);
            }
        }

        // Deduplicate — Claude may return the same operation more than once
        return apis
            .GroupBy(a => a.ApiName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// Extracts baseUrl from Claude-generated connector metadata.
    /// Placeholder tokens (e.g. "{instance_url}") are kept as-is for later credential substitution.
    /// </summary>
    private static string ExtractBaseUrl(string metadata)
    {
        try
        {
            var doc = JsonDocument.Parse(metadata);
            if (doc.RootElement.TryGetProperty("baseUrl", out var bu))
                return (bu.GetString() ?? string.Empty).TrimEnd('/');
        }
        catch (JsonException) { }

        return string.Empty;
    }
}
