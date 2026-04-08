using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.GenerateConnectorAssets;

/// <summary>
/// Discovers all API operations supported by the connector and persists them to TenantConnectorApis.
/// Single responsibility: populate the API catalogue for a connector.
/// Workflow generation is handled separately by GenerateMcpWorkflowsCommandHandler.
/// </summary>
public sealed class GenerateConnectorAssetsCommandHandler(
    ITenantConnectorRepository repository,
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
                """{"type":"object","properties":{"method":{"type":"string"},"path":{"type":"string"},"description":{"type":"string"},"requestBody":{"type":"object"},"responseSchema":{"type":"object"}},"required":["method","path","description"]}""")
        };

        var prompt = $"""
            You are an API discovery agent for a workflow automation platform.
            Based on the following connector metadata and info for "{connector.ConnectorName}",
            enumerate all API operations this connector supports.

            METADATA:
            {connector.Metadata}

            INFO:
            {connector.Info}

            Use the create_api_operation tool for EVERY API operation this connector supports.
            Be exhaustive — include all available endpoints, not just the most common ones.
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

        // API operations → TenantConnectorApis
        var apiOperationJsons = toolCalls
            .Where(t => t.ToolName == "create_api_operation")
            .Select(t => t.InputJson)
            .ToList();

        var apiList = BuildApiList(connector, apiOperationJsons);
        if (apiList.Count > 0)
            await repository.AddApisAsync(apiList, cancellationToken);

        return new GenerateConnectorAssetsResult(
            JsonSerializer.Serialize(apiOperationJsons),
            "[]");
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
