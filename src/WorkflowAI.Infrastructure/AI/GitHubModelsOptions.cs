namespace WorkflowAI.Infrastructure.AI;

public sealed class GitHubModelsOptions
{
    public const string SectionName = "GitHubModels";

    /// <summary>GitHub Personal Access Token with 'models:read' permission (or GitHub Copilot subscription).</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>GitHub Models inference endpoint. Default is the public endpoint.</summary>
    public string Endpoint { get; set; } = "https://models.inference.ai.azure.com";

    /// <summary>Model to use on GitHub Models. Claude is not available; use "gpt-4o" or "Meta-Llama-3.1-405B-Instruct".</summary>
    public string DefaultModel { get; set; } = "gpt-4o";
}
