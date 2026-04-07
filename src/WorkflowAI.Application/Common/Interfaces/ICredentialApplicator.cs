namespace WorkflowAI.Application.Common.Interfaces;

public interface ICredentialApplicator
{
    string AuthType { get; }
    void Apply(HttpRequestMessage request, IReadOnlyDictionary<string, string> fields);
}
