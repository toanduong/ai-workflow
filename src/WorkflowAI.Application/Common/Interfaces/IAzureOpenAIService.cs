namespace WorkflowAI.Application.Common.Interfaces;

public interface IAzureOpenAIService
{
    Task<AICompletionResult> CompleteAsync(string prompt, string model, CancellationToken cancellationToken = default);
    Task<AICompletionResult> CompleteWithToolsAsync(string prompt, string model, IReadOnlyList<AIToolDefinition> tools, CancellationToken cancellationToken = default);
}

public sealed record AICompletionResult(string Content, int TokensUsed, bool Success, string? ErrorMessage = null);

public sealed record AIToolDefinition(string Name, string Description, string ParametersJson);
