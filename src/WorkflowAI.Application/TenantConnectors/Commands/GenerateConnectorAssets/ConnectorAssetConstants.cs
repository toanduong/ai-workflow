namespace WorkflowAI.Application.TenantConnectors.Commands.GenerateConnectorAssets;

/// <summary>
/// Constants for connector asset generation.
/// </summary>
public static class ConnectorAssetConstants
{
    /// <summary>
    /// Tool name for Claude to call when defining an API operation.
    /// </summary>
    public const string CreateApiOperationToolName = "create_api_operation";

    /// <summary>
    /// JSON schema for the create_api_operation tool input.
    /// </summary>
    public const string CreateApiOperationSchema = """
        {
          "type": "object",
          "properties": {
            "method": { "type": "string", "description": "HTTP verb: GET, POST, PUT, PATCH, DELETE, or WEBHOOK" },
            "path": { "type": "string", "description": "URL path relative to baseUrl, or event type for WEBHOOK" },
            "description": { "type": "string", "description": "What this operation does — be specific about the resource and action" },
            "requestBody": { "type": "object", "description": "JSON schema of the request body (omit for GET/DELETE)" },
            "responseSchema": { "type": "object", "description": "JSON schema of the response" }
          },
          "required": ["method", "path", "description"]
        }
        """;

    /// <summary>
    /// Error code for asset generation failures.
    /// </summary>
    public const string AssetGenerationFailedCode = "TenantConnector.AssetGenerationFailed";

    /// <summary>
    /// Valid HTTP methods that Claude should generate.
    /// </summary>
    public static readonly string[] ValidHttpMethods = { "GET", "POST", "PUT", "PATCH", "DELETE", "WEBHOOK" };
}
