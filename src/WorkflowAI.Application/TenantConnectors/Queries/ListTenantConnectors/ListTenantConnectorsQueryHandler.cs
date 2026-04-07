using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Queries.ListTenantConnectors;

public sealed class ListTenantConnectorsQueryHandler(ITenantConnectorRepository repository)
    : IRequestHandler<ListTenantConnectorsQuery, Result<IReadOnlyList<TenantConnectorDto>>>
{
    public async Task<Result<IReadOnlyList<TenantConnectorDto>>> Handle(
        ListTenantConnectorsQuery request, CancellationToken ct)
    {
        var connectors = await repository.GetByTenantAsync(TenantId.From(request.TenantId), ct);

        var dtos = connectors.Select(c => new TenantConnectorDto(
            c.Id.Value,
            c.TenantId.Value,
            c.ConnectorName,
            c.Metadata,
            c.Info,
            c.Status.Name,
            c.FailureReason,
            c.CreatedAt,
            c.UpdatedAt)).ToList();

        return Result.Success<IReadOnlyList<TenantConnectorDto>>(dtos);
    }
}
