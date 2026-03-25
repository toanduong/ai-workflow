using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Connectors.Commands.RefreshConnectorToken;

public sealed record RefreshConnectorTokenCommand(Guid ConnectorId) : IRequest<Result>;
