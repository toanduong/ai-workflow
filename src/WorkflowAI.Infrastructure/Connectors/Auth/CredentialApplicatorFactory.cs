using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors.Auth;

public sealed class CredentialApplicatorFactory(IEnumerable<ICredentialApplicator> applicators)
    : ICredentialApplicatorFactory
{
    public ICredentialApplicator Resolve(string authType) =>
        applicators.FirstOrDefault(a => a.AuthType.Equals(authType, StringComparison.OrdinalIgnoreCase))
        ?? applicators.First(a => a.AuthType == "Default");
}
