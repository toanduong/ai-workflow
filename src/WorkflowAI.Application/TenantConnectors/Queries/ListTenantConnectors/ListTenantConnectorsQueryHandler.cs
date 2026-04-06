using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Queries.ListTenantConnectors;

public sealed class ListTenantConnectorsQueryHandler(ITenantConnectorRepository repository)
    : IRequestHandler<ListTenantConnectorsQuery, Result<IReadOnlyList<TenantConnectorDto>>>
{
    public async Task<Result<IReadOnlyList<TenantConnectorDto>>> Handle(
        ListTenantConnectorsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(request.TenantId);
        var connectors = await repository.GetByTenantAsync(tenantId, cancellationToken);

        var dtos = connectors.Select(c => new TenantConnectorDto(
            c.Id.Value, c.TenantId.Value, c.ConnectorName,
            c.Metadata, c.Info, c.Status.Name,
            c.CreatedAt, c.UpdatedAt)).ToList().AsReadOnly();

        return Result<IReadOnlyList<TenantConnectorDto>>.Success(dtos);
    }
}
