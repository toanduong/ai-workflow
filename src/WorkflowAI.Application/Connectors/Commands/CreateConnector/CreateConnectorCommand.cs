using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.Connectors.Commands.CreateConnector;

public sealed record CreateConnectorCommand(
    string Name,
    string ConnectorType,
    string AuthModel,
    string? Configuration,
    string? ManagedApiId,
    string? Secret) : IRequest<Result<CreateConnectorResult>>;

public sealed record CreateConnectorResult(Guid ConnectorId, string? OAuthConsentUrl);
