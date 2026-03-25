using MediatR;
using WorkflowAI.Application.Connectors.Queries.ListConnectors;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.Connectors.Queries.GetConnector;

public sealed class GetConnectorQueryHandler(IConnectorRepository connectorRepository)
    : IRequestHandler<GetConnectorQuery, Result<ConnectorDto>>
{
    public async Task<Result<ConnectorDto>> Handle(GetConnectorQuery request, CancellationToken ct)
    {
        var connector = await connectorRepository.GetByIdAsync(ConnectorId.From(request.ConnectorId), ct);
        if (connector is null)
            return Error.NotFound("Connector.NotFound", "Connector not found.");

        return new ConnectorDto(
            connector.Id.Value, connector.Name, connector.ConnectorType.Name,
            connector.AuthModel.Name, connector.Status.Name, connector.AzureApiConnectionId,
            connector.Configuration, connector.CreatedAt, connector.ExpiresAt);
    }
}
