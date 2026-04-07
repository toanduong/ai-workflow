using System.Net.Http.Headers;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors.Auth;

public sealed class OAuth2CredentialApplicator : ICredentialApplicator
{
    public string AuthType => "OAuth2";

    public void Apply(HttpRequestMessage request, IReadOnlyDictionary<string, string> fields)
    {
        if (fields.TryGetValue("access_token", out var token) ||
            fields.TryGetValue("token", out token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
