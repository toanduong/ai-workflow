using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.ValidateTenantConnector;

public sealed class ValidateTenantConnectorCommandHandler(
    ITenantConnectorRepository repository,
    IAnthropicService anthropicService,
    IConnectorHttpValidator httpValidator,
    ILogger<ValidateTenantConnectorCommandHandler> logger)
    : IRequestHandler<ValidateTenantConnectorCommand, Result<ValidateTenantConnectorResult>>
{
    private const string ApiDiscoveryPromptTemplate = """
        You are an API operations mapper for a workflow automation platform.
        The connector "{connectorName}" is now validated and active at base URL: {baseUrl}
        Auth type: {authType}

        List all key API operations this service supports. For each operation return a JSON array entry:
        {
          "apiName": "resource/action",
          "httpMethod": "GET | POST | PUT | DELETE | PATCH",
          "urlTemplate": "{baseUrl}/path/to/resource",
          "metadata": {
            "headers": { "<appropriate-auth-header-for-authType>": "$secret" },
            "requestMapping": { },
            "responseMapping": { }
          }
        }

        Use the correct auth header for the authType:
          - APIKey  → "X-Api-Key": "$secret"
          - Bearer  → "Authorization": "Bearer $secret"
          - OAuth2  → "Authorization": "Bearer $secret"
          - Basic   → "Authorization": "Basic $secret"

        Focus on the most commonly used operations (up to 20).
        Return ONLY a valid JSON array, no markdown, no extra text.
        """;

    public async Task<Result<ValidateTenantConnectorResult>> Handle(
        ValidateTenantConnectorCommand request, CancellationToken ct)
    {
        var connector = await repository.GetByIdAsync(
            TenantConnectorId.From(request.TenantConnectorId), ct);

        if (connector is null)
            return Error.NotFound("TenantConnector.NotFound", "Connector not found.");

        var baseUrl = ResolveBaseUrl(connector.Metadata, request.Credentials);
        var apiKey  = ResolveApiKey(request.Credentials);

        var (testUrl, testMethod) = ExtractTestEndpoint(connector.Metadata, baseUrl);

        var (isValid, failureReason) = await httpValidator.TestAsync(testUrl, testMethod, apiKey, ct);

        if (!isValid)
        {
            connector.MarkFailed(failureReason ?? "Connection test failed.");
            await repository.UpdateAsync(connector, ct);
            return new ValidateTenantConnectorResult(false, 0, failureReason);
        }

        connector.Activate();
        await repository.UpdateAsync(connector, ct);

        var apis = await DiscoverApiOperationsAsync(connector, baseUrl, ct);

        if (apis.Count > 0)
        {
            await repository.DeleteApisByConnectorAsync(connector.Id, ct);
            await repository.AddApisAsync(apis, ct);
        }

        return new ValidateTenantConnectorResult(true, apis.Count);
    }

    /// <summary>
    /// Resolves the base URL for the connector.
    ///
    /// Claude may generate one of two patterns in Metadata.baseUrl:
    ///   - Fixed URL   → "https://api.apollo.io"       (SaaS, same for all tenants)
    ///   - Template    → "{instance_url}"               (self-hosted, differs per tenant)
    ///
    /// Template placeholders are substituted from the credentials dict at runtime.
    /// This means code never needs to know which services are self-hosted.
    /// </summary>
    private static string ResolveBaseUrl(string metadata, IReadOnlyDictionary<string, string> credentials)
    {
        try
        {
            var doc = JsonDocument.Parse(metadata);
            if (doc.RootElement.TryGetProperty("baseUrl", out var bu))
            {
                var baseUrl = bu.GetString() ?? string.Empty;

                // Substitute any {placeholder} with the matching credential value
                foreach (var (key, value) in credentials)
                    baseUrl = baseUrl.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);

                if (baseUrl.StartsWith("http://",  StringComparison.OrdinalIgnoreCase) ||
                    baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return baseUrl.TrimEnd('/');
            }
        }
        catch (JsonException) { }

        // Last resort: if any credential value is a URL (user passed it despite Claude not templating),
        // use the first one — allows the system to still work with a non-ideal Claude response.
        return credentials.Values
            .FirstOrDefault(v =>
                v.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                v.StartsWith("http://",  StringComparison.OrdinalIgnoreCase))
            ?.TrimEnd('/')
            ?? string.Empty;
    }

    /// <summary>
    /// Returns the first credential value that is not a URL — used as the authentication key/token.
    /// The auth field name (api_key, access_token, etc.) is intentionally not hardcoded here;
    /// it is determined by Claude's requiredFields and provided by the caller.
    /// </summary>
    private static string ResolveApiKey(IReadOnlyDictionary<string, string> credentials) =>
        credentials.Values.FirstOrDefault(v =>
            !v.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !v.StartsWith("http://",  StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

    private static (string Url, string Method) ExtractTestEndpoint(string metadata, string baseUrl)
    {
        try
        {
            var doc = JsonDocument.Parse(metadata);
            if (doc.RootElement.TryGetProperty("testEndpoint", out var ep))
            {
                var path   = ep.TryGetProperty("path",   out var p) ? p.GetString() : null;
                var method = ep.TryGetProperty("method", out var m) ? m.GetString() : "GET";

                if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(baseUrl))
                    return ($"{baseUrl}{path}", method?.ToUpperInvariant() ?? "GET");
            }
        }
        catch (JsonException) { }

        // Last resort: probe the base URL directly
        return (baseUrl, "GET");
    }

    private async Task<List<TenantConnectorApi>> DiscoverApiOperationsAsync(
        TenantConnector connector, string baseUrl, CancellationToken ct)
    {
        var authType = ExtractAuthType(connector.Metadata);
        var prompt = ApiDiscoveryPromptTemplate
            .Replace("{connectorName}", connector.ConnectorName)
            .Replace("{baseUrl}", baseUrl)
            .Replace("{authType}", authType);

        var aiResult = await anthropicService.CompleteAsync(prompt, cancellationToken: ct);
        if (!aiResult.Success)
        {
            logger.LogWarning("Claude API discovery failed for {Connector}", connector.ConnectorName);
            return [];
        }

        return ParseApiOperations(connector, aiResult.Content);
    }

    private static string ExtractAuthType(string metadata)
    {
        try
        {
            var doc = JsonDocument.Parse(metadata);
            if (doc.RootElement.TryGetProperty("authType", out var at))
                return at.GetString() ?? "APIKey";
        }
        catch (JsonException) { }
        return "APIKey";
    }

    private static string StripMarkdownFences(string content)
    {
        if (content.StartsWith("```"))
        {
            var firstNewline = content.IndexOf('\n');
            var lastFence = content.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                return content[(firstNewline + 1)..lastFence].Trim();
        }
        return content;
    }

    private static List<TenantConnectorApi> ParseApiOperations(TenantConnector connector, string content)
    {
        var apis = new List<TenantConnectorApi>();
        try
        {
            var array = JsonDocument.Parse(StripMarkdownFences(content.Trim())).RootElement;
            if (array.ValueKind != JsonValueKind.Array) return apis;

            foreach (var item in array.EnumerateArray())
            {
                var apiName    = item.TryGetProperty("apiName",    out var n) ? n.GetString() : null;
                var httpMethod = item.TryGetProperty("httpMethod",  out var m) ? m.GetString() : null;
                var urlTpl     = item.TryGetProperty("urlTemplate", out var u) ? u.GetString() : null;
                var meta       = item.TryGetProperty("metadata",    out var d) ? d.GetRawText() : "{}";

                if (string.IsNullOrEmpty(apiName) || string.IsNullOrEmpty(httpMethod) || string.IsNullOrEmpty(urlTpl))
                    continue;

                apis.Add(TenantConnectorApi.Create(
                    connector.Id, connector.TenantId, connector.ConnectorName,
                    apiName, httpMethod, urlTpl, meta));
            }
        }
        catch (JsonException) { }

        return apis;
    }
}
