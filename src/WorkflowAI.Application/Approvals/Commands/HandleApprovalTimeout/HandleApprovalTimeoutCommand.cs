using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Approvals.Commands.HandleApprovalTimeout;

public sealed record HandleApprovalTimeoutCommand() : IRequest<Result<int>>;
