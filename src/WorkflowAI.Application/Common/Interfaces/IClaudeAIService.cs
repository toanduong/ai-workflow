namespace WorkflowAI.Application.Common.Interfaces;

public interface IClaudeAIService
{
    Task<AICompletionResult> CompleteAsync(string prompt, CancellationToken cancellationToken = default);
    Task<AICompletionResult> CompleteWithToolsAsync(string prompt, IReadOnlyList<AIToolDefinition> tools, CancellationToken cancellationToken = default);
}
