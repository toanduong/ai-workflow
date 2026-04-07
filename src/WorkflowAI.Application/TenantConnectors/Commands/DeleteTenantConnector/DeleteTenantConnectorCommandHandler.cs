using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.DeleteTenantConnector;

public sealed class DeleteTenantConnectorCommandHandler(
    ITenantConnectorRepository repository)
    : IRequestHandler<DeleteTenantConnectorCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        DeleteTenantConnectorCommand request, CancellationToken ct)
    {
        var id = TenantConnectorId.From(request.TenantConnectorId);

        var connector = await repository.GetByIdAsync(id, ct);
        if (connector is null)
            return Error.NotFound("TenantConnector.NotFound", "Connector not found.");

        // Remove discovered API operations first (Table 2), then the connector (Table 1)
        await repository.DeleteApisByConnectorAsync(id, ct);
        await repository.DeleteAsync(id, ct);

        return true;
    }
}
