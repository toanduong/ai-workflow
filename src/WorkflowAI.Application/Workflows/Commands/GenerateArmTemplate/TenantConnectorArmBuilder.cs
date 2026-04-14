using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace WorkflowAI.Application.Workflows.Commands.GenerateArmTemplate;

/// <summary>
/// Builds an Azure Logic App ARM template for HTTP-based tenant connector workflows.
/// Unlike ArmTemplateBuilder (which handles Microsoft.Web/connections managed APIs),
/// this builder constructs generic HTTP actions with runtime-resolved credentials.
/// </summary>
public sealed class TenantConnectorArmBuilder
{
    private readonly JsonObject _armParameters = new();
    private readonly JsonObject _definitionParameters = new();
    private readonly JsonObject _actions = new();
    private string? _previousActionName;

    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    /// <summary>
    /// Adds a securestring ARM parameter (e.g. an API key or instance URL).
    /// </summary>
    public void AddParameter(string paramName, bool secure = true)
    {
        _armParameters[paramName] = new JsonObject
        {
            ["type"] = secure ? "securestring" : "string"
        };

        _definitionParameters[paramName] = new JsonObject
        {
            ["type"] = secure ? "securestring" : "string",
            ["defaultValue"] = $"@{{{{{$"parameters('{paramName}')"}}}}}"
        };
    }

    /// <summary>
    /// Adds an HTTP action step to the workflow definition.
    /// </summary>
    /// <param name="actionName">Unique name for this action (e.g. "ListContacts").</param>
    /// <param name="method">HTTP verb (GET, POST, etc.).</param>
    /// <param name="resolvedUri">
    ///     The URI with Logic App expressions substituted, e.g.
    ///     "@{concat(parameters('odoo_instance_url'), '/api/res.partner')}"
    /// </param>
    /// <param name="headers">Key/value pairs for HTTP headers. Values may contain expressions.</param>
    /// <param name="body">Optional request body expression for POST/PUT/PATCH.</param>
    public void AddStep(
        string actionName,
        string method,
        string resolvedUri,
        IReadOnlyDictionary<string, string>? headers = null,
        string? body = null)
    {
        var inputs = new JsonObject
        {
            ["method"] = method.ToUpperInvariant(),
            ["uri"] = resolvedUri
        };

        if (headers is { Count: > 0 })
        {
            var headersNode = new JsonObject();
            foreach (var (k, v) in headers)
                headersNode[k] = v;
            inputs["headers"] = headersNode;
        }

        if (!string.IsNullOrEmpty(body))
            inputs["body"] = body;

        var action = new JsonObject
        {
            ["type"] = "Http",
            ["inputs"] = inputs,
            ["runAfter"] = _previousActionName is null
                ? new JsonObject()
                : new JsonObject
                {
                    [_previousActionName] = new JsonArray { "Succeeded" }
                }
        };

        _actions[actionName] = action;
        _previousActionName = actionName;
    }

    /// <summary>
    /// Renders the full ARM deployment template JSON.
    /// </summary>
    public string Build(string workflowName)
    {
        var definition = new JsonObject
        {
            ["$schema"] = "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
            ["contentVersion"] = "1.0.0.0",
            ["parameters"] = _definitionParameters,
            ["triggers"] = new JsonObject
            {
                ["manual"] = new JsonObject
                {
                    ["type"] = "Request",
                    ["kind"] = "Http",
                    ["inputs"] = new JsonObject
                    {
                        ["schema"] = new JsonObject()
                    }
                }
            },
            ["actions"] = _actions
        };

        var logicApp = new JsonObject
        {
            ["type"] = "Microsoft.Logic/workflows",
            ["apiVersion"] = "2019-05-01",
            ["name"] = workflowName,
            ["location"] = "[resourceGroup().location]",
            ["properties"] = new JsonObject
            {
                ["state"] = "Enabled",
                ["definition"] = definition,
                ["parameters"] = BuildRuntimeParameters()
            }
        };

        var template = new JsonObject
        {
            ["$schema"] = "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
            ["contentVersion"] = "1.0.0.0",
            ["parameters"] = _armParameters,
            ["resources"] = new JsonArray { logicApp }
        };

        return template.ToJsonString(IndentedOptions);
    }

    /// <summary>
    /// Converts a URL template like "https://{instance_url}/api/res.partner" into a
    /// Logic App expression "@{concat(parameters('connector_instance_url'), '/api/res.partner')}".
    /// Literal URL parts between placeholders are handled automatically.
    /// </summary>
    public static string ResolveUrlTemplate(string urlTemplate, string paramPrefix)
    {
        // Find all {placeholder} tokens
        var placeholders = Regex.Matches(urlTemplate, @"\{(\w+)\}");
        if (placeholders.Count == 0)
            return urlTemplate;

        // Build a concat() expression
        var parts = new List<string>();
        int cursor = 0;

        foreach (Match m in placeholders)
        {
            if (m.Index > cursor)
            {
                var literal = urlTemplate[cursor..m.Index];
                if (!string.IsNullOrEmpty(literal))
                    parts.Add($"'{EscapeSingleQuotes(literal)}'");
            }

            var paramName = BuildParamName(paramPrefix, m.Groups[1].Value);
            parts.Add($"parameters('{paramName}')");
            cursor = m.Index + m.Length;
        }

        if (cursor < urlTemplate.Length)
        {
            var trailing = urlTemplate[cursor..];
            if (!string.IsNullOrEmpty(trailing))
                parts.Add($"'{EscapeSingleQuotes(trailing)}'");
        }

        return parts.Count == 1
            ? $"@{{{parts[0]}}}"
            : $"@{{concat({string.Join(", ", parts)})}}";
    }

    /// <summary>
    /// Builds authentication headers based on the connector's authType.
    /// Supported types: APIKey, Bearer, Basic, OAuth2.
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildAuthHeaders(
        string authType,
        IReadOnlyList<string> requiredFields,
        string paramPrefix)
    {
        var headers = new Dictionary<string, string>();

        switch (authType.ToLowerInvariant())
        {
            case "apikey":
            {
                // Common field names for API keys
                var keyField = requiredFields.FirstOrDefault(f =>
                    f.Contains("key", StringComparison.OrdinalIgnoreCase) ||
                    f.Contains("token", StringComparison.OrdinalIgnoreCase))
                    ?? requiredFields.FirstOrDefault()
                    ?? "api_key";
                var paramName = BuildParamName(paramPrefix, keyField);
                headers["x-api-key"] = $"@{{parameters('{paramName}')}}";
                break;
            }
            case "bearer":
            {
                var tokenField = requiredFields.FirstOrDefault(f =>
                    f.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                    f.Contains("key", StringComparison.OrdinalIgnoreCase))
                    ?? requiredFields.FirstOrDefault()
                    ?? "access_token";
                var paramName = BuildParamName(paramPrefix, tokenField);
                headers["Authorization"] = $"@{{concat('Bearer ', parameters('{paramName}'))}}";
                break;
            }
            case "basic":
            {
                var userField = requiredFields.FirstOrDefault(f =>
                    f.Contains("user", StringComparison.OrdinalIgnoreCase) ||
                    f.Contains("login", StringComparison.OrdinalIgnoreCase))
                    ?? "username";
                var passField = requiredFields.FirstOrDefault(f =>
                    f.Contains("pass", StringComparison.OrdinalIgnoreCase) ||
                    f.Contains("secret", StringComparison.OrdinalIgnoreCase))
                    ?? "password";
                var userParam = BuildParamName(paramPrefix, userField);
                var passParam = BuildParamName(paramPrefix, passField);
                headers["Authorization"] =
                    $"@{{concat('Basic ', base64(concat(parameters('{userParam}'), ':', parameters('{passParam}'))))}}";
                break;
            }
            case "oauth2":
            {
                var tokenField = requiredFields.FirstOrDefault(f =>
                    f.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                    f.Contains("access", StringComparison.OrdinalIgnoreCase))
                    ?? requiredFields.FirstOrDefault()
                    ?? "access_token";
                var paramName = BuildParamName(paramPrefix, tokenField);
                headers["Authorization"] = $"@{{concat('Bearer ', parameters('{paramName}'))}}";
                break;
            }
        }

        return headers;
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private JsonObject BuildRuntimeParameters()
    {
        var runtimeParams = new JsonObject();
        foreach (var key in _armParameters)
        {
            runtimeParams[key.Key] = new JsonObject
            {
                ["value"] = $"[parameters('{key.Key}')]"
            };
        }
        return runtimeParams;
    }

    private static string BuildParamName(string prefix, string field) =>
        $"{prefix}_{field}".ToLowerInvariant().Replace("-", "_");

    private static string EscapeSingleQuotes(string s) =>
        s.Replace("'", "''");

    /// <summary>
    /// Gets all registered parameters with values populated from credentials.
    /// If credentials dictionary is provided, uses those values; otherwise returns empty strings.
    /// </summary>
    public Dictionary<string, string> GetParameters(Dictionary<string, string>? credentials = null)
    {
        if (credentials == null || credentials.Count == 0)
        {
            // Fallback to empty strings if no credentials provided
            return _armParameters.Select(kvp => kvp.Key).ToDictionary(
                key => key,
                _ => string.Empty);
        }

        // Populate actual credential values from the provided dictionary
        return _armParameters.Select(kvp => kvp.Key).ToDictionary(
            key => key,
            key => credentials.TryGetValue(key, out var value) ? value : string.Empty);
    }
}
