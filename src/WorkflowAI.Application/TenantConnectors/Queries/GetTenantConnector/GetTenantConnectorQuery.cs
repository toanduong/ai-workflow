using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.TenantConnectors.Queries.GetTenantConnector;

public sealed record GetTenantConnectorQuery(Guid TenantConnectorId)
    : IRequest<Result<TenantConnectorDto>>;
