using System.Net.Http.Headers;
using System.Text;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors.Auth;

/// <summary>HTTP Basic auth — base64(username:password). Fields: "username" + "password".</summary>
public sealed class BasicCredentialApplicator : ICredentialApplicator
{
    public string AuthType => "Basic";

    public void Apply(HttpRequestMessage request, IReadOnlyDictionary<string, string> credentials)
    {
        if (credentials.TryGetValue("username", out var username) &&
            credentials.TryGetValue("password", out var password))
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }
    }
}
