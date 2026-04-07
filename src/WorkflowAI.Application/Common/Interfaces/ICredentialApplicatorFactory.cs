namespace WorkflowAI.Application.Common.Interfaces;

/// <summary>
/// Resolves the correct ICredentialApplicator for a given authType string.
/// authType comes from Claude-generated connector Metadata — no hardcoding needed.
/// </summary>
public interface ICredentialApplicatorFactory
{
    ICredentialApplicator Resolve(string authType);
}
