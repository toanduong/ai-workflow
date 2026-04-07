using System.Net.Http.Headers;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors.Auth;

public sealed class BearerCredentialApplicator : ICredentialApplicator
{
    public string AuthType => "Bearer";

    public void Apply(HttpRequestMessage request, IReadOnlyDictionary<string, string> fields)
    {
        if (fields.TryGetValue("token", out var token) ||
            fields.TryGetValue("access_token", out token) ||
            fields.TryGetValue("bearer_token", out token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
