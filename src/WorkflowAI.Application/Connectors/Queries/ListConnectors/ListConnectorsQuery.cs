using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Connectors.Queries.ListConnectors;

public sealed record ListConnectorsQuery() : IRequest<Result<IReadOnlyList<ConnectorDto>>>;
