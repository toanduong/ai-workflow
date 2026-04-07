namespace WorkflowAI.Infrastructure.AI;

public sealed class ClaudeAIOptions
{
    public const string SectionName = "Claude";
    public string Model { get; set; } = "claude-opus-4-6";
    public int MaxTokens { get; set; } = 16000;
}
