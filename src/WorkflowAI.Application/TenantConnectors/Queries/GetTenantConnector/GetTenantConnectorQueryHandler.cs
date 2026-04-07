using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Queries.GetTenantConnector;

public sealed class GetTenantConnectorQueryHandler(ITenantConnectorRepository repository)
    : IRequestHandler<GetTenantConnectorQuery, Result<TenantConnectorDto>>
{
    public async Task<Result<TenantConnectorDto>> Handle(
        GetTenantConnectorQuery request, CancellationToken ct)
    {
        var connector = await repository.GetByIdAsync(
            TenantConnectorId.From(request.TenantConnectorId), ct);

        if (connector is null)
            return Error.NotFound("TenantConnector.NotFound", "Connector not found.");

        return new TenantConnectorDto(
            connector.Id.Value,
            connector.TenantId.Value,
            connector.ConnectorName,
            connector.Metadata,
            connector.Info,
            connector.Status.Name,
            connector.FailureReason,
            connector.CreatedAt,
            connector.UpdatedAt);
    }
}
