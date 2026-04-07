using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Queries.ListTenantConnectors;

public sealed class ListTenantConnectorsQueryHandler(
    ITenantConnectorRepository repository,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListTenantConnectorsQuery, Result<IReadOnlyList<TenantConnectorDto>>>
{
    public async Task<Result<IReadOnlyList<TenantConnectorDto>>> Handle(
        ListTenantConnectorsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated)
            return Error.Unauthorized("Auth.Required", "User must be authenticated.");
        var tenantId = TenantId.From(request.TenantId);
        var connectors = await repository.GetByTenantAsync(tenantId, cancellationToken);

        var dtos = connectors.Select(c => new TenantConnectorDto(
            c.Id.Value, c.TenantId.Value, c.ConnectorName,
            c.Metadata, c.Info, c.Status.Name,
            c.CreatedAt, c.UpdatedAt)).ToList().AsReadOnly();

        return Result<IReadOnlyList<TenantConnectorDto>>.Success(dtos);
    }
}
