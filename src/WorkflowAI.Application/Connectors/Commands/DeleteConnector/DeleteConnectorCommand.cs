using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Connectors.Commands.DeleteConnector;

public sealed record DeleteConnectorCommand(Guid ConnectorId) : IRequest<Result>;
