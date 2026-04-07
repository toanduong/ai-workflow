using System.Net.Http.Headers;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors.Auth;

/// <summary>OAuth2 — same wire format as Bearer. Field: "access_token" or "token".</summary>
public sealed class OAuth2CredentialApplicator : ICredentialApplicator
{
    public string AuthType => "OAuth2";

    public void Apply(HttpRequestMessage request, IReadOnlyDictionary<string, string> credentials)
    {
        if (credentials.TryGetValue("access_token", out var token) ||
            credentials.TryGetValue("token",        out token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
