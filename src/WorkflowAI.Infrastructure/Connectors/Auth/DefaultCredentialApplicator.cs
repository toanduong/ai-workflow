using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors.Auth;

/// <summary>Fallback applicator: adds each field as a request header.</summary>
public sealed class DefaultCredentialApplicator : ICredentialApplicator
{
    public string AuthType => "Default";

    public void Apply(HttpRequestMessage request, IReadOnlyDictionary<string, string> fields)
    {
        foreach (var (key, value) in fields)
            request.Headers.TryAddWithoutValidation(key, value);
    }
}
