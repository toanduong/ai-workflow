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
            "method": { "type": "string", "description": "HTTP method (GET, POST, PUT, PATCH, DELETE)" },
            "path": { "type": "string", "description": "API endpoint path" },
            "description": { "type": "string", "description": "What this API operation does" },
            "requestBody": { "type": "object", "description": "Request body schema" },
            "responseSchema": { "type": "object", "description": "Response schema" }
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
    public static readonly string[] ValidHttpMethods = { "GET", "POST", "PUT", "PATCH", "DELETE" };
}
