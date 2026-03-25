using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Connectors.Commands.CompleteOAuthFlow;

public sealed record CompleteOAuthFlowCommand(string Code, string State) : IRequest<Result>;
