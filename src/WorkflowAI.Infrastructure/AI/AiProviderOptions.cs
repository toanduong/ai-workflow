namespace WorkflowAI.Infrastructure.AI;

public sealed class AiProviderOptions
{
    public const string SectionName = "Ai";

    /// <summary>
    /// Which AI backend to use for IAnthropicService.
    /// Supported values: "Anthropic" (default) | "GitHubModels"
    /// </summary>
    public string Provider { get; set; } = "Anthropic";
}
