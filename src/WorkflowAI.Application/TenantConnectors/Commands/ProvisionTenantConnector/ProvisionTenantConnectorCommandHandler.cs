using System.Text.Json;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkflowAI.Application.Common;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.ProvisionTenantConnector;

public sealed partial class ProvisionTenantConnectorCommandHandler(
    ITenantConnectorRepository repository,
    IClaudeAIService claudeAIService,
    IOptions<ClaudePromptOptions> promptOptions,
    ICurrentUserService currentUserService,
    ILogger<ProvisionTenantConnectorCommandHandler> logger)
    : IRequestHandler<ProvisionTenantConnectorCommand, Result<ProvisionTenantConnectorResult>>
{
    [GeneratedRegex(@"[^a-zA-Z0-9 _\-]")]
    private static partial Regex SafeConnectorNamePattern();

    public async Task<Result<ProvisionTenantConnectorResult>> Handle(
        ProvisionTenantConnectorCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated)
            return Error.Unauthorized("Auth.Required", "User must be authenticated.");

        var tenantId = TenantId.From(request.TenantId);

        var existing = await repository.GetByTenantAndConnectorAsync(
            tenantId, request.ConnectorName, cancellationToken);

        if (existing is not null)
            return Error.Conflict("TenantConnector.AlreadyExists",
                $"Connector '{request.ConnectorName}' already exists for this tenant.");

        var safeConnectorName = SafeConnectorNamePattern().Replace(request.ConnectorName, string.Empty);
        var prompt = promptOptions.Value.ConnectorMetadataTemplate
            .Replace("{connectorName}", safeConnectorName);

        var aiResult = await claudeAIService.CompleteAsync(prompt, cancellationToken);

        if (!aiResult.Success)
        {
            logger.LogError("Claude failed to generate metadata for connector '{ConnectorName}': {Error}",
                request.ConnectorName, aiResult.ErrorMessage);
            return Error.Unexpected("TenantConnector.AIGenerationFailed",
                aiResult.ErrorMessage ?? "Claude failed to generate connector metadata.");
        }

        if (!TryParseClaudeResponse(aiResult.Content, out var metadata, out var info))
        {
            logger.LogError("Claude returned invalid JSON for connector '{ConnectorName}'. Content: {Content}",
                request.ConnectorName, aiResult.Content);
            return Error.Unexpected("TenantConnector.InvalidAIResponse",
                "Claude returned an invalid JSON response.");
        }

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
