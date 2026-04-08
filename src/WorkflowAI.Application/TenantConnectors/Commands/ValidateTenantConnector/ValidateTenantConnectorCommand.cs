using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.TenantConnectors.Commands.ValidateTenantConnector;

public sealed record ValidateTenantConnectorCommand(
    Guid TenantId,
    Guid TenantConnectorId,
    IReadOnlyDictionary<string, string> Credentials  // field names match requiredFields from Claude-generated Metadata
) : IRequest<Result<bool>>;
