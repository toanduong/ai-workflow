using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.Connectors.Commands.ValidateConnector;

public sealed class ValidateConnectorCommandHandler(
    IConnectorRepository connectorRepository,
    IConnectorService connectorService)
    : IRequestHandler<ValidateConnectorCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ValidateConnectorCommand request, CancellationToken ct)
    {
        var connector = await connectorRepository.GetByIdAsync(ConnectorId.From(request.ConnectorId), ct);
        if (connector is null)
            return Error.NotFound("Connector.NotFound", "Connector not found.");

        connector.StartValidation();
        var isValid = await connectorService.ValidateConnectorAsync(connector, ct);
        if (isValid) connector.Activate(connector.ExpiresAt);
        else connector.MarkFailed();
        await connectorRepository.UpdateAsync(connector, ct);
        return isValid;
    }
}
