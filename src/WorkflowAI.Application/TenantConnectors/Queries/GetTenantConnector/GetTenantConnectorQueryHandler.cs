using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Queries.GetTenantConnector;

public sealed class GetTenantConnectorQueryHandler(ITenantConnectorRepository repository)
    : IRequestHandler<GetTenantConnectorQuery, Result<TenantConnectorDto>>
{
    public async Task<Result<TenantConnectorDto>> Handle(
        GetTenantConnectorQuery request, CancellationToken cancellationToken)
    {
        var id = TenantConnectorId.From(request.TenantConnectorId);
        var connector = await repository.GetByIdAsync(id, cancellationToken);

        if (connector is null)
            return Error.NotFound("TenantConnector.NotFound",
                $"TenantConnector {request.TenantConnectorId} not found.");

        return new TenantConnectorDto(
            connector.Id.Value, connector.TenantId.Value, connector.ConnectorName,
            connector.Metadata, connector.Info, connector.Status.Name,
            connector.CreatedAt, connector.UpdatedAt);
    }
}
