using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Queries.GetTenantConnector;

public sealed class GetTenantConnectorQueryHandler(
    ITenantConnectorRepository repository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetTenantConnectorQuery, Result<TenantConnectorDto>>
{
    public async Task<Result<TenantConnectorDto>> Handle(
        GetTenantConnectorQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated)
            return Error.Unauthorized("Auth.Required", "User must be authenticated.");
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
