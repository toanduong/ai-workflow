namespace WorkflowAI.Infrastructure.AI;

public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string ApiKey { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = "claude-sonnet-4-5";
}
