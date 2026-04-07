using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.TenantConnectors.Commands.ProvisionTenantConnector;

public sealed record ProvisionTenantConnectorCommand(
    Guid TenantId,
    string ConnectorName   // plain string — no enum, fully dynamic (e.g. "Odoo", "Apollo", "Salesforce")
) : IRequest<Result<ProvisionTenantConnectorResult>>;

public sealed record ProvisionTenantConnectorResult(
    Guid TenantConnectorId,
    string ConnectorName,
    string Metadata,
    string Info
);
