namespace WorkflowAI.Application.Common.Interfaces;

public interface ICredentialApplicatorFactory
{
    ICredentialApplicator Resolve(string authType);
}
