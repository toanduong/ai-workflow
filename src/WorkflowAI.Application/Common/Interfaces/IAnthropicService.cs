namespace WorkflowAI.Application.Common.Interfaces;

// Reuses AICompletionResult and AIToolDefinition defined in IAzureOpenAIService.cs

public interface IAnthropicService
{
    Task<AICompletionResult> CompleteAsync(
        string prompt,
        string? model = null,
        CancellationToken cancellationToken = default);

    Task<AICompletionResult> CompleteWithToolsAsync(
        string prompt,
        IReadOnlyList<AIToolDefinition> tools,
        string? model = null,
        CancellationToken cancellationToken = default);
}
