namespace WorkflowAI.Application.Common;

public sealed class ClaudePromptOptions
{
    public const string SectionName = "ClaudePrompts";

    public string ConnectorMetadataTemplate { get; set; } = """
        You are a connector metadata generator for a workflow automation platform.
        For the 3rd party service "{connectorName}", generate two JSON objects:

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
}
