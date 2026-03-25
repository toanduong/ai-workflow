using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.AI;

public sealed class AzureOpenAIService(
    IOptions<AzureOpenAIOptions> options,
    ILogger<AzureOpenAIService> logger) : IAzureOpenAIService
{
    private readonly AzureOpenAIClient _client = new(
        new Uri(options.Value.Endpoint),
        new System.ClientModel.ApiKeyCredential(options.Value.ApiKey));

    public async Task<AICompletionResult> CompleteAsync(string prompt, string model, CancellationToken cancellationToken = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(model);
            var response = await chatClient.CompleteChatAsync(
                [new UserChatMessage(prompt)],
                cancellationToken: cancellationToken);

            var content = response.Value.Content[0].Text;
            var tokens = response.Value.Usage.TotalTokenCount;

            return new AICompletionResult(content, tokens, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Azure OpenAI completion failed for model {Model}", model);
            return new AICompletionResult(string.Empty, 0, false, ex.Message);
        }
    }

    public async Task<AICompletionResult> CompleteWithToolsAsync(string prompt, string model,
        IReadOnlyList<AIToolDefinition> tools, CancellationToken cancellationToken = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(model);

            var chatTools = tools.Select(t =>
                ChatTool.CreateFunctionTool(t.Name, t.Description, BinaryData.FromString(t.ParametersJson))).ToList();

            var chatOptions = new ChatCompletionOptions();
            foreach (var tool in chatTools)
                chatOptions.Tools.Add(tool);

            var response = await chatClient.CompleteChatAsync(
                [new UserChatMessage(prompt)],
                chatOptions,
                cancellationToken);

            var content = response.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
            var tokens = response.Value.Usage.TotalTokenCount;

            return new AICompletionResult(content, tokens, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Azure OpenAI tool completion failed for model {Model}", model);
            return new AICompletionResult(string.Empty, 0, false, ex.Message);
        }
    }
}
