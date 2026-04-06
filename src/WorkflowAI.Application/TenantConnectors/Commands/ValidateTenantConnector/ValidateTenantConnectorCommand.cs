using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.TenantConnectors.Commands.ValidateTenantConnector;

public sealed record ValidateTenantConnectorCommand(
    Guid TenantConnectorId,
    Dictionary<string, string> CredentialFields) : IRequest<Result<bool>>;
