using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.TenantConnectors.Queries.ListTenantConnectors;

public sealed record ListTenantConnectorsQuery(Guid TenantId) : IRequest<Result<IReadOnlyList<TenantConnectorDto>>>;
