using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors.Auth;

/// <summary>
/// Fallback applicator: adds every credential field as a request header.
/// Used when Claude generates an authType the system has no specific handler for.
/// </summary>
public sealed class DefaultCredentialApplicator : ICredentialApplicator
{
    public string AuthType => "Default";

    public void Apply(HttpRequestMessage request, IReadOnlyDictionary<string, string> credentials)
    {
        foreach (var (key, value) in credentials)
            request.Headers.TryAddWithoutValidation(key, value);
    }
}
