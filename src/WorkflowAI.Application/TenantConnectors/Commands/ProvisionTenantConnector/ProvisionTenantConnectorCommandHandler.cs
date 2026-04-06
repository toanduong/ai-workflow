using System.Text.Json;
using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.ProvisionTenantConnector;

public sealed class ProvisionTenantConnectorCommandHandler(
    ITenantConnectorRepository repository,
    IClaudeAIService claudeAIService)
    : IRequestHandler<ProvisionTenantConnectorCommand, Result<ProvisionTenantConnectorResult>>
{
    private const string PromptTemplate = """
        You are a connector metadata generator for a workflow automation platform.
        For the 3rd party service "{0}", generate two JSON objects:

        1. METADATA: Technical integration details including:
           - authType: one of (APIKey|OAuth2|Basic|Bearer)
           - requiredFields: array of field names needed for connection (e.g. ["api_key"])
           - endpoints: object with key API endpoints, each having "method" and "path"
           - configSchema: JSON Schema object for configuration fields
           - testEndpoint: object with "method" and "path" to use for connection validation

        2. INFO: Human-readable information including:
           - description: what the service does (one sentence)
           - docsUrl: official API documentation URL
           - capabilities: array of things that can be automated
           - rateLimits: string describing known rate limits
           - webhookSupport: boolean

        Return ONLY valid JSON in exactly this format with no extra text:
        {"metadata": {...}, "info": {...}}
        """;

    public async Task<Result<ProvisionTenantConnectorResult>> Handle(
        ProvisionTenantConnectorCommand request, CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(request.TenantId);

        var existing = await repository.GetByTenantAndConnectorAsync(
            tenantId, request.ConnectorName, cancellationToken);

        if (existing is not null)
            return Error.Conflict("TenantConnector.AlreadyExists",
                $"Connector '{request.ConnectorName}' already exists for this tenant.");

        var prompt = PromptTemplate.Replace("{0}", request.ConnectorName);
        var aiResult = await claudeAIService.CompleteAsync(prompt, cancellationToken);

        if (!aiResult.Success)
            return Error.Unexpected("TenantConnector.AIGenerationFailed",
                aiResult.ErrorMessage ?? "Claude failed to generate connector metadata.");

        if (!TryParseClaudeResponse(aiResult.Content, out var metadata, out var info))
            return Error.Unexpected("TenantConnector.InvalidAIResponse",
                "Claude returned an invalid JSON response.");

        var connector = TenantConnector.Create(tenantId, request.ConnectorName);
        connector.SetMetadata(metadata, info);

        await repository.AddAsync(connector, cancellationToken);

        return new ProvisionTenantConnectorResult(connector.Id.Value, metadata, info);
    }

    private static bool TryParseClaudeResponse(string content, out string metadata, out string info)
    {
        metadata = string.Empty;
        info = string.Empty;

        try
        {
            var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (!root.TryGetProperty("metadata", out var metadataElement) ||
                !root.TryGetProperty("info", out var infoElement))
                return false;

            metadata = metadataElement.GetRawText();
            info = infoElement.GetRawText();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
