using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.AI;

public sealed class AnthropicService(
    IChatClient chatClient,
    IOptions<AnthropicOptions> options,
    ILogger<AnthropicService> logger) : IAnthropicService
{
    public async Task<AICompletionResult> CompleteAsync(
        string prompt,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var chatOptions = new ChatOptions
            {
                ModelId = model ?? options.Value.DefaultModel
            };

            var response = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                chatOptions,
                cancellationToken);

            var content = response.Text ?? string.Empty;
            var tokens = (int)(response.Usage?.TotalTokenCount ?? 0);

            return new AICompletionResult(content, tokens, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Anthropic completion failed for model {Model}",
                model ?? options.Value.DefaultModel);
            return new AICompletionResult(string.Empty, 0, false, ex.Message);
        }
    }

    public async Task<AICompletionResult> CompleteWithToolsAsync(
        string prompt,
        IReadOnlyList<AIToolDefinition> tools,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // CreateDeclaration passes the full JSON schema to the model so it knows what params to provide
            var aiTools = tools
                .Select(t => (AITool)AIFunctionFactory.CreateDeclaration(
                    t.Name,
                    t.Description,
                    JsonDocument.Parse(t.ParametersJson).RootElement))
                .ToList();

            var chatOptions = new ChatOptions
            {
                ModelId = model ?? options.Value.DefaultModel,
                Tools = aiTools,
                ToolMode = ChatToolMode.RequireAny
            };

            var response = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                chatOptions,
                cancellationToken);

            var content = response.Text ?? string.Empty;
            var tokens = (int)(response.Usage?.TotalTokenCount ?? 0);

            // Extract tool calls from all response messages
            var toolCalls = response.Messages
                .SelectMany(m => m.Contents)
                .OfType<FunctionCallContent>()
                .Select(fc => new AIToolCall(fc.Name, JsonSerializer.Serialize(fc.Arguments)))
                .ToList();

            return new AICompletionResult(content, tokens, true,
                ToolCalls: toolCalls.Count > 0 ? toolCalls : null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Anthropic tool completion failed for model {Model}",
                model ?? options.Value.DefaultModel);
            return new AICompletionResult(string.Empty, 0, false, ex.Message);
        }
    }
}
