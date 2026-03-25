using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Connectors.Commands.ValidateConnector;

public sealed record ValidateConnectorCommand(Guid ConnectorId) : IRequest<Result<bool>>;
