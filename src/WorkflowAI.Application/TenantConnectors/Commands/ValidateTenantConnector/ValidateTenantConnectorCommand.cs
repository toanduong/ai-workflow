using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.TenantConnectors.Commands.ValidateTenantConnector;

public sealed record ValidateTenantConnectorCommand(
    Guid TenantConnectorId,
    IReadOnlyDictionary<string, string> Credentials  // field names match requiredFields from Claude-generated Metadata
) : IRequest<Result<ValidateTenantConnectorResult>>;

public sealed record ValidateTenantConnectorResult(
    bool IsValid,
    int ApiOperationsDiscovered,
    string? FailureReason = null
);
