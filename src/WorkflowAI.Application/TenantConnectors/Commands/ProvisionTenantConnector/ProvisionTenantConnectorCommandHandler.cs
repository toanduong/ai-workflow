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
          "authType": "<see authType rules>",
          "protocol": "<see protocol rules>",
          "requiredFields": ["field1", "field2"],
          "baseUrl": "<see baseUrl rules>",
          "testEndpoint": {{ "method": "GET|POST", "path": "/...", "body": "...", "contentType": "..." }},
          "configSchema": {{
            "field1": {{ "type": "string", "description": "...", "required": true }}
          }}
        }}

        Rules for "authType" (pick exactly one):
          - "APIKey"  — credentials sent as a header (X-Api-Key, api-key, etc.)
          - "Bearer"  — credentials sent as Authorization: Bearer {token}
          - "Basic"   — credentials sent as Authorization: Basic base64(login:password)
          - "OAuth2"  — OAuth2 flow with client_id + client_secret
          - "XMLRpc"  — XML-RPC protocol (Odoo, legacy ERP systems)
          - "NoAuth"  — public API, no auth required

        Rules for "protocol" (pick exactly one):
          - "REST"    — standard HTTP REST with JSON (most SaaS: HubSpot, Apollo, Salesforce, Slack, etc.)
          - "XMLRpc"  — XML-RPC (Odoo, legacy ERPs)
          - "GraphQL" — GraphQL (Shopify, GitHub, etc.)
          - "SOAP"    — SOAP/WSDL (legacy enterprise systems)

        Rules for "requiredFields":
          - Include ONLY the fields the end user must provide to connect (credentials, URLs).
          - Use these exact field names by convention:
              API key        → "api_key"
              Bearer token   → "access_token"
              Instance URL   → "instance_url"   (self-hosted, URL differs per customer)
              Base URL       → "base_url"        (self-hosted alternative)
              Username/login → "username"
              Password       → "password"
              Client ID      → "client_id"
              Client secret  → "client_secret"

        Rules for "baseUrl":
          - SaaS / cloud-hosted (same URL for all customers): use the real URL
              Apollo    → "https://api.apollo.io"
              HubSpot   → "https://api.hubapi.com"
              Salesforce → "https://login.salesforce.com"
          - Self-hosted or per-tenant (URL differs per customer): use a placeholder
              Odoo      → "{instance_url}"   (matches field name in requiredFields)
              Chatwoot  → "{base_url}"
              Mattermost → "{instance_url}"
          - The placeholder must exactly match a field name in requiredFields.

        Rules for "testEndpoint":
          - Use the simplest endpoint that confirms auth works and returns HTTP 2xx.
          - Prefer a health/ping/version/me endpoint that requires no parameters.
          - "path" must be relative (starts with /). Full URL = baseUrl + path.
          - For REST APIs:
              {{ "method": "GET", "path": "/v1/account" }}
          - For XML-RPC services (Odoo): use the version endpoint which needs no auth
              {{ "method": "POST", "path": "/xmlrpc/2/common",
                 "body": "<?xml version='1.0'?><methodCall><methodName>version</methodName><params/></methodCall>",
                 "contentType": "text/xml" }}
          - For GraphQL:
              {{ "method": "POST", "path": "/graphql",
                 "body": "{{\"query\":\"{{ __typename }}\"}}", "contentType": "application/json" }}

        2. INFO — human-readable details:
        {{
          "description": "One sentence: what this service does and who uses it",
          "docsUrl": "https://developers.example.com/api",
          "capabilities": ["list", "of", "key", "features"],
          "rateLimits": "e.g. 100 requests/10s, 1000/day",
          "webhookSupport": true,
          "apiVersion": "v3"
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
