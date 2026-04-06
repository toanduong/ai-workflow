using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.TenantConnectors.Commands.ProvisionTenantConnector;

public sealed record ProvisionTenantConnectorCommand(
    Guid TenantId,
    string ConnectorName) : IRequest<Result<ProvisionTenantConnectorResult>>;

public sealed record ProvisionTenantConnectorResult(
    Guid TenantConnectorId,
    string Metadata,
    string Info);
