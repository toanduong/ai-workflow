using MediatR;
using WorkflowAI.Application.Connectors.Queries.ListConnectors;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Connectors.Queries.GetConnector;

public sealed record GetConnectorQuery(Guid ConnectorId) : IRequest<Result<ConnectorDto>>;
