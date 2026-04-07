using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors.Auth;

public sealed class ApiKeyCredentialApplicator : ICredentialApplicator
{
    public string AuthType => "APIKey";

    public void Apply(HttpRequestMessage request, IReadOnlyDictionary<string, string> fields)
    {
        if (fields.TryGetValue("api_key", out var apiKey) ||
            fields.TryGetValue("apiKey", out apiKey))
        {
            request.Headers.Add("X-Api-Key", apiKey);
        }
    }
}
