using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Queries.GetTenantConnectorApis;

public sealed class GetTenantConnectorApisQueryHandler(ITenantConnectorRepository repository)
    : IRequestHandler<GetTenantConnectorApisQuery, Result<IReadOnlyList<TenantConnectorApiDto>>>
{
    public async Task<Result<IReadOnlyList<TenantConnectorApiDto>>> Handle(
        GetTenantConnectorApisQuery request, CancellationToken ct)
    {
        var apis = await repository.GetApisByConnectorAsync(
            TenantConnectorId.From(request.TenantConnectorId), ct);

        var dtos = apis.Select(a => new TenantConnectorApiDto(
            a.Id.Value,
            a.TenantConnectorId.Value,
            a.ConnectorType,
            a.ApiName,
            a.HttpMethod,
            a.UrlTemplate,
            a.Metadata,
            a.CreatedAt)).ToList();

        return Result.Success<IReadOnlyList<TenantConnectorApiDto>>(dtos);
    }
}
