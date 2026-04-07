using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors.Auth;

/// <summary>APIKey auth — adds X-Api-Key header. Field name: "api_key" or "apiKey".</summary>
public sealed class ApiKeyCredentialApplicator : ICredentialApplicator
{
    public string AuthType => "APIKey";

    public void Apply(HttpRequestMessage request, IReadOnlyDictionary<string, string> credentials)
    {
        if (credentials.TryGetValue("api_key", out var key) ||
            credentials.TryGetValue("apiKey",  out key))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", key);
        }
    }
}
