using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    IOptions<ConnectorAssetGenerationOptions> options,
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
        if (existingApis.Any())
        {
            logger.LogInformation(
                "API operations already exist for connector {ConnectorId}, skipping generation",
                request.TenantConnectorId);
            return new GenerateConnectorAssetsResult(
                JsonSerializer.Serialize(existingApis.Select(a => a.Metadata).ToList()),
                "[]");
        }

        logger.LogInformation("Generating connector assets for {ConnectorId} ({ConnectorType})",
            request.TenantConnectorId, connector.ConnectorType);

        var tools = new[]
        {
            new AIToolDefinition(
                ConnectorAssetConstants.CreateApiOperationToolName,
                "Register one API operation supported by this connector. Call once per operation.",
                ConnectorAssetConstants.CreateApiOperationSchema)
        };

        var prompt = BuildDiscoveryPrompt(connector.ConnectorType, connector.Metadata, connector.Info);

        var aiResult = await claudeAIService.CompleteWithToolsAsync(prompt, tools, cancellationToken: cancellationToken);

        if (!aiResult.Success)
        {
            logger.LogError("Claude failed to generate assets for connector {ConnectorId}: {Error}",
                request.TenantConnectorId, aiResult.ErrorMessage);
            return Error.Unexpected(
                ConnectorAssetConstants.AssetGenerationFailedCode,
                aiResult.ErrorMessage ?? "Claude failed to generate connector assets.");
        }

        var toolCalls = aiResult.ToolCalls ?? [];

        // API operations → TenantConnectorApis
        var apiOperationJsons = toolCalls
            .Where(t => t.ToolName == ConnectorAssetConstants.CreateApiOperationToolName)
            .Select(t => t.InputJson)
            .ToList();

        var apiList = BuildApiList(connector, apiOperationJsons);

        // Validate API count against limits
        if (apiList.Count > options.Value.MaxApisPerConnector)
        {
            logger.LogError(
                "Generated {Count} APIs exceeds maximum {Max} for connector {ConnectorId}",
                apiList.Count, options.Value.MaxApisPerConnector, connector.Id);
            return Error.Validation(
                "TenantConnector.TooManyApis",
                $"Generated {apiList.Count} APIs but maximum is {options.Value.MaxApisPerConnector}");
        }

        if (apiList.Count > options.Value.WarningThresholdApiCount)
        {
            logger.LogWarning(
                "Generated {ApiCount} APIs (>{Threshold}) for connector {ConnectorId}",
                apiList.Count, options.Value.WarningThresholdApiCount, connector.Id);
        }

        if (apiList.Count > 0)
            await repository.AddApisAsync(apiList, cancellationToken);

        logger.LogInformation("Generated and persisted {Count} API operations for connector {ConnectorId}",
            apiList.Count, connector.Id);

        return new GenerateConnectorAssetsResult(
            JsonSerializer.Serialize(apiList.Select(a => new { a.ApiName, a.HttpMethod, a.UrlTemplate })),
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

                // Extract and validate method
                if (!doc.TryGetProperty("method", out var methodElement))
                {
                    logger.LogWarning("API operation missing 'method' field for connector {ConnectorId}",
                        connector.Id);
                    continue;
                }

                var method = methodElement.GetString()?.ToUpperInvariant() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(method))
                {
                    logger.LogWarning("API operation has empty 'method' for connector {ConnectorId}",
                        connector.Id);
                    continue;
                }

                if (!ConnectorAssetConstants.ValidHttpMethods.Contains(method))
                {
                    logger.LogWarning(
                        "API operation has invalid HTTP method '{Method}' for connector {ConnectorId}",
                        method, connector.Id);
                    continue;
                }

                // Extract and validate path
                if (!doc.TryGetProperty("path", out var pathElement))
                {
                    logger.LogWarning("API operation missing 'path' field for connector {ConnectorId}",
                        connector.Id);
                    continue;
                }

                var path = pathElement.GetString();
                if (string.IsNullOrWhiteSpace(path))
                {
                    logger.LogWarning("API operation has empty 'path' for connector {ConnectorId}",
                        connector.Id);
                    continue;
                }

                var urlTemplate = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? path
                    : $"{baseUrl}{path}";

                apis.Add(TenantConnectorApi.Create(
                    connector.Id,
                    connector.TenantId,
                    connector.ConnectorType,
                    apiName: $"{method} {path}",
                    httpMethod: method,
                    urlTemplate: urlTemplate,
                    metadata: opJson));
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Skipping malformed API operation JSON for connector {ConnectorId}",
                    connector.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error processing API operation for connector {ConnectorId}",
                    connector.Id);
            }
        }

        // Deduplicate — Claude may return the same operation more than once
        var deduplicated = apis
            .GroupBy(a => a.ApiName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (deduplicated.Count < apis.Count)
        {
            logger.LogInformation(
                "Deduplicated {DuplicateCount} duplicate APIs for connector {ConnectorId}",
                apis.Count - deduplicated.Count, connector.Id);
        }

        return deduplicated;
    }

    private static string BuildDiscoveryPrompt(string connectorName, string metadata, string info)
    {
        return
            "You are an API discovery agent for a workflow automation platform.\n" +
            $"Your job is to enumerate ALL operations supported by the \"{connectorName}\" connector.\n\n" +
            "CONNECTOR METADATA:\n" +
            metadata + "\n\n" +
            "CONNECTOR INFO:\n" +
            info + "\n\n" +
            "Call the create_api_operation tool once for EVERY API operation this connector supports.\n" +
            "Be exhaustive — include all available endpoints.\n\n" +
            "Rules for each operation:\n" +
            "1. \"method\" — the actual HTTP verb: GET, POST, PUT, PATCH, DELETE, or WEBHOOK\n" +
            "2. \"path\" — the URL path relative to baseUrl. Use {param}-style placeholders for dynamic segments.\n" +
            "      REST example:    /crm/v3/objects/contacts\n" +
            "      REST with param: /crm/v3/objects/contacts/{contactId}\n" +
            "      XML-RPC (Odoo):  /xmlrpc/2/object  (all Odoo calls go here — put model+method in description)\n" +
            "      GraphQL:         /graphql           (single endpoint — put operation name in description)\n" +
            "3. \"description\" — be specific: include the resource name and what it does.\n" +
            "      Good: \"Search contacts by email, name, or company using filter operators\"\n" +
            "      Bad:  \"Search contacts\"\n" +
            "      For XML-RPC: \"Odoo res.partner search_read — list contacts and companies\"\n" +
            "      For GraphQL: \"GitHub query: list repositories for authenticated user\"\n" +
            "4. \"requestBody\" — JSON schema of the request body (omit for GET/DELETE).\n" +
            "5. \"responseSchema\" — JSON schema of the response (omit if unknown).\n\n" +
            "Protocol-specific guidance:\n" +
            "- REST (HubSpot, Apollo, Salesforce, Slack, etc.): one tool call per endpoint+method combination.\n" +
            "- XML-RPC (Odoo): path is always /xmlrpc/2/object. Create one operation per model+method pair.\n" +
            "      e.g. description: \"Odoo res.partner search_read\", \"Odoo sale.order create\"\n" +
            "- GraphQL (Shopify, GitHub): path is always /graphql. Create one operation per query/mutation.\n" +
            "- Webhooks: create operations with method WEBHOOK and path = the event type.\n" +
            "      e.g. path: \"contact.creation\", \"deal.propertyChange\"\n";
    }

    /// <summary>
    /// Extracts baseUrl from Claude-generated connector metadata.
    /// Placeholder tokens (e.g. "{instance_url}") are kept as-is for later credential substitution.
    /// Returns empty string if not found or metadata is invalid JSON.
    /// </summary>
    private string ExtractBaseUrl(string metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(metadata);
            if (doc.RootElement.TryGetProperty("baseUrl", out var bu))
            {
                var baseUrl = bu.GetString();
                if (!string.IsNullOrWhiteSpace(baseUrl))
                    return baseUrl.TrimEnd('/');
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to extract baseUrl from connector metadata");
        }

        return string.Empty;
    }
}
