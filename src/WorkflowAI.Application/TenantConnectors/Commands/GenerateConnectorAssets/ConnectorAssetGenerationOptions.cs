namespace WorkflowAI.Application.TenantConnectors.Commands.GenerateConnectorAssets;

/// <summary>
/// Configuration options for connector asset (API operation) generation via Claude.
/// </summary>
public sealed class ConnectorAssetGenerationOptions
{
    public const string SectionName = "ConnectorAssetGeneration";

    /// <summary>
    /// The prompt template for Claude to discover API operations.
    /// Placeholders: {ConnectorType}, {Metadata}, {Info}
    /// </summary>
    public string PromptTemplate { get; set; } = """
        You are an API discovery agent for a workflow automation platform.
        Based on the following connector metadata and info for "{ConnectorType}",
        enumerate all API operations this connector supports.

        METADATA:
        {Metadata}

        INFO:
        {Info}

        Use the create_api_operation tool for EVERY API operation this connector supports.
        Be exhaustive — include all available endpoints, not just the most common ones.
        """;

    /// <summary>
    /// Maximum number of API operations to generate per connector.
    /// Prevents runaway generation that could hit token limits or cost limits.
    /// </summary>
    public int MaxApisPerConnector { get; set; } = 100;

    /// <summary>
    /// Whether to log a warning if API count exceeds this threshold.
    /// </summary>
    public int WarningThresholdApiCount { get; set; } = 50;
}
