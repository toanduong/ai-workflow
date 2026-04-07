using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.DeleteTenantConnector;

public sealed class DeleteTenantConnectorCommandHandler(
    ITenantConnectorRepository repository)
    : IRequestHandler<DeleteTenantConnectorCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        DeleteTenantConnectorCommand request, CancellationToken cancellationToken)
    {
        var id = TenantConnectorId.From(request.TenantConnectorId);
        var connector = await repository.GetByIdAsync(id, cancellationToken);

        if (connector is null)
            return Error.NotFound("TenantConnector.NotFound",
                $"TenantConnector {request.TenantConnectorId} not found.");

        // Table 2 must be cleared before Table 1 to respect FK constraint
        await repository.DeleteApisByConnectorAsync(id, cancellationToken);
        await repository.DeleteAsync(id, cancellationToken);
        return true;
    }
}
