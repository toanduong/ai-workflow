using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.TenantConnectors.Queries.GetTenantConnectorApis;

public sealed record GetTenantConnectorApisQuery(Guid TenantConnectorId)
    : IRequest<Result<IReadOnlyList<TenantConnectorApiDto>>>;
