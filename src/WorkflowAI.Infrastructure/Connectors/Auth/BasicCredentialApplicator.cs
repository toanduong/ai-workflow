using System.Net.Http.Headers;
using System.Text;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors.Auth;

public sealed class BasicCredentialApplicator : ICredentialApplicator
{
    public string AuthType => "Basic";

    public void Apply(HttpRequestMessage request, IReadOnlyDictionary<string, string> fields)
    {
        if (fields.TryGetValue("username", out var username) &&
            fields.TryGetValue("password", out var password))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
    }
}
