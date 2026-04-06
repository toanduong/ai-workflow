using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.AI;

public sealed class ClaudeAIService(
    AnthropicClient client,
    ILogger<ClaudeAIService> logger) : IClaudeAIService
{
    public async Task<AICompletionResult> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            var totalTokens = 0L;
            var contentBuilder = new System.Text.StringBuilder();

            await foreach (var streamEvent in client.Messages.CreateStreaming(
                new MessageCreateParams
                {
                    Model = "claude-opus-4-6",
                    MaxTokens = 16000,
                    Thinking = new ThinkingConfigAdaptive(),
                    Messages = [new() { Role = Role.User, Content = prompt }]
                }, cancellationToken: cancellationToken))
            {
                if (streamEvent.TryPickContentBlockDelta(out var delta) &&
                    delta.Delta.TryPickText(out var text))
                {
                    contentBuilder.Append(text.Text);
                }

                if (streamEvent.TryPickDelta(out var msgDelta) &&
                    msgDelta.Usage is { } usage)
                {
                    totalTokens = usage.OutputTokens;
                }
            }

            return new AICompletionResult(contentBuilder.ToString(), (int)totalTokens, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Claude AI completion failed");
            return new AICompletionResult(string.Empty, 0, false, ex.Message);
        }
    }

    public async Task<AICompletionResult> CompleteWithToolsAsync(
        string prompt,
        IReadOnlyList<AIToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var claudeTools = tools.Select(t => (ToolUnion)new Tool
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = new InputSchema
                {
                    Properties = System.Text.Json.JsonSerializer
                        .Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(t.ParametersJson)
                        ?? []
                }
            }).ToList();

            var messages = new List<MessageParam>
            {
                new() { Role = Role.User, Content = prompt }
            };

            var contentBuilder = new System.Text.StringBuilder();
            var totalTokens = 0L;
            var allToolCalls = new List<AIToolCall>();

            while (true)
            {
                var response = await client.Messages.Create(
                    new MessageCreateParams
                    {
                        Model = "claude-opus-4-6",
                        MaxTokens = 16000,
                        Thinking = new ThinkingConfigAdaptive(),
                        Tools = claudeTools,
                        Messages = messages
                    }, cancellationToken: cancellationToken);

                totalTokens += response.Usage.OutputTokens;

                foreach (var block in response.Content)
                {
                    if (block.TryPickText(out var textBlock))
                        contentBuilder.Append(textBlock.Text);
                }

                if (response.StopReason != StopReason.ToolUse)
                    break;

                var toolUseBlocks = response.Content
                    .Select(b => b.Value)
                    .OfType<ToolUseBlock>()
                    .ToList();

                // Capture tool call inputs before sending back
                foreach (var tu in toolUseBlocks)
                {
                    var inputJson = System.Text.Json.JsonSerializer.Serialize(tu.Input);
                    allToolCalls.Add(new AIToolCall(tu.Name, inputJson));
                }

                // Collect tool use blocks for the assistant turn
                var assistantBlocks = toolUseBlocks
                    .Select(tu => (ContentBlockParam)new ToolUseBlockParam
                    {
                        ID = tu.ID,
                        Name = tu.Name,
                        Input = tu.Input
                    })
                    .ToList();

                messages.Add(new MessageParam
                {
                    Role = Role.Assistant,
                    Content = new MessageParamContent(assistantBlocks)
                });

                // Add placeholder tool results
                var toolResults = toolUseBlocks
                    .Select(tu => (ContentBlockParam)new ToolResultBlockParam
                    {
                        ToolUseID = tu.ID,
                        Content = new ToolResultBlockParamContent(
                            $"[Tool '{tu.Name}' invoked successfully]")
                    })
                    .ToList();

                messages.Add(new MessageParam
                {
                    Role = Role.User,
                    Content = new MessageParamContent(toolResults)
                });
            }

            return new AICompletionResult(
                contentBuilder.ToString(), (int)totalTokens, true,
                ToolCalls: allToolCalls);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Claude AI tool completion failed");
            return new AICompletionResult(string.Empty, 0, false, ex.Message);
        }
    }
}
