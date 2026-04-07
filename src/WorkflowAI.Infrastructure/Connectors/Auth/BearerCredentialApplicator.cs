using System.Net.Http.Headers;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors.Auth;

/// <summary>Bearer token auth — adds Authorization: Bearer header. Field: "token", "access_token", or "bearer_token".</summary>
public sealed class BearerCredentialApplicator : ICredentialApplicator
{
    public string AuthType => "Bearer";

    public void Apply(HttpRequestMessage request, IReadOnlyDictionary<string, string> credentials)
    {
        if (credentials.TryGetValue("token",        out var token) ||
            credentials.TryGetValue("access_token", out token)     ||
            credentials.TryGetValue("bearer_token", out token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
