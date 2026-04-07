using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.TenantConnectors.Commands.DeleteTenantConnector;

public sealed record DeleteTenantConnectorCommand(
    Guid TenantConnectorId
) : IRequest<Result<bool>>;
