namespace WorkflowAI.Application.Common.Interfaces;

/// <summary>
/// Applies credentials to an outgoing HTTP request based on a specific auth type.
/// Claude generates the authType in connector Metadata — the factory resolves the correct applicator.
/// </summary>
public interface ICredentialApplicator
{
    string AuthType { get; }
    void Apply(HttpRequestMessage request, IReadOnlyDictionary<string, string> credentials);
}
