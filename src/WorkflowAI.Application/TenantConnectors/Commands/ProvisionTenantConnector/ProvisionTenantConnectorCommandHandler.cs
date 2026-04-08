using System.Text.Json;
using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.ProvisionTenantConnector;

public sealed class ProvisionTenantConnectorCommandHandler(
    ITenantConnectorRepository repository,
    IAnthropicService anthropicService)
    : IRequestHandler<ProvisionTenantConnectorCommand, Result<ProvisionTenantConnectorResult>>
{
    private const string MetadataPromptTemplate = """
        You are a connector metadata generator for a workflow automation platform.
        For the 3rd party service "{connectorName}", generate exactly two JSON objects.

        1. METADATA — technical integration details:
        {{
          "authType": "APIKey | OAuth2 | Basic | Bearer",
          "requiredFields": ["field1", "field2"],
          "baseUrl": "<see rules below>",
          "testEndpoint": {{ "method": "GET", "path": "/api/health" }},
          "configSchema": {{
            "field1": {{ "type": "string", "description": "...", "required": true }}
          }}
        }}

        Rules for "baseUrl":
          - SaaS / cloud-hosted services (fixed URL for all users):
              Use the actual base URL, e.g. "https://api.apollo.io"
          - Self-hosted or per-tenant services (URL differs per customer):
              Use a placeholder that matches the field name in requiredFields,
              e.g. "{instance_url}" — the platform substitutes it at runtime.
          Examples: Apollo → "https://api.apollo.io"
                    Odoo  → "{instance_url}"  (with "instance_url" in requiredFields)
                    Chatwoot → "{base_url}"   (with "base_url" in requiredFields)

        Rules for "testEndpoint":
          - Use the simplest endpoint that confirms the credentials are valid and returns HTTP 2xx.
          - Prefer a dedicated health/ping/version endpoint if one exists.
          - If the endpoint requires a request body (e.g. JSON-RPC services like Odoo), include:
              "body": "{}", "contentType": "application/json"
          - Examples:
              GET  /health            → {{ "method": "GET",  "path": "/health" }}
              POST /web/webclient/version_info (Odoo JSON-RPC) →
                   {{ "method": "POST", "path": "/web/webclient/version_info",
                      "body": "{{}}", "contentType": "application/json" }}

        2. INFO — human-readable details:
        {{
          "description": "What this service does",
          "docsUrl": "https://docs.example.com/api",
          "capabilities": ["capability1", "capability2"],
          "rateLimits": "e.g. 1000 requests/hour",
          "webhookSupport": true
        }}

        Return ONLY valid JSON in this exact format, no markdown, no extra text:
        {{"metadata": {{...}}, "info": {{...}}}}
        """;

    public async Task<Result<ProvisionTenantConnectorResult>> Handle(
        ProvisionTenantConnectorCommand request, CancellationToken ct)
    {
        var tenantId = TenantId.From(request.TenantId);

        // Conflict check — one connector per tenant per name
        var existing = await repository.GetByTenantAndNameAsync(tenantId, request.ConnectorType, ct);
        if (existing is not null)
            return Error.Conflict(
                "TenantConnector.AlreadyExists",
                $"Connector '{request.ConnectorType}' already exists for this tenant.");

        // Call Claude to generate Metadata + Info for this connector
        var prompt = MetadataPromptTemplate.Replace("{connectorName}", request.ConnectorType);
        var aiResult = await anthropicService.CompleteAsync(prompt, cancellationToken: ct);

        if (!aiResult.Success)
            return Error.Unexpected(
                "TenantConnector.AiFailed",
                $"Claude failed to generate metadata: {aiResult.ErrorMessage}");

        // Parse Claude's response
        var (metadata, info) = ParseAiResponse(aiResult.Content);
        if (metadata is null || info is null)
            return Error.Unexpected(
                "TenantConnector.InvalidAiResponse",
                "Claude returned an unexpected response format.");

        // Create and persist
        var connector = TenantConnector.Create(tenantId, request.ConnectorType);
        connector.SetMetadata(metadata, info);

        await repository.AddAsync(connector, ct);

        return new ProvisionTenantConnectorResult(
            connector.Id.Value,
            connector.ConnectorType,
            connector.Metadata,
            connector.Info);
    }

    private static (string? Metadata, string? Info) ParseAiResponse(string content)
    {
        try
        {
            // Claude sometimes wraps JSON in markdown code fences — strip them
            var json = StripMarkdownFences(content.Trim());

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("metadata", out var metadataEl) ||
                !root.TryGetProperty("info", out var infoEl))
                return (null, null);

            return (metadataEl.GetRawText(), infoEl.GetRawText());
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string StripMarkdownFences(string content)
    {
        // Remove ```json ... ``` or ``` ... ``` wrappers
        if (content.StartsWith("```"))
        {
            var firstNewline = content.IndexOf('\n');
            var lastFence = content.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                return content[(firstNewline + 1)..lastFence].Trim();
        }
        return content;
    }
}
