using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.Connectors.Queries.ListConnectors;

public sealed class ListConnectorsQueryHandler(IConnectorRepository connectorRepository)
    : IRequestHandler<ListConnectorsQuery, Result<IReadOnlyList<ConnectorDto>>>
{
    public async Task<Result<IReadOnlyList<ConnectorDto>>> Handle(ListConnectorsQuery request, CancellationToken ct)
    {
        var connectors = await connectorRepository.GetAllAsync(ct);
        var dtos = connectors.Select(c => new ConnectorDto(
            c.Id.Value, c.Name, c.ConnectorType.Name, c.AuthModel.Name,
            c.Status.Name, c.AzureApiConnectionId, c.Configuration,
            c.CreatedAt, c.ExpiresAt)).ToList().AsReadOnly();
        return Result<IReadOnlyList<ConnectorDto>>.Success(dtos);
    }
}
