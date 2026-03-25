using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Approvals.Commands.ProcessApproval;

public sealed record ProcessApprovalCommand(
    Guid ApprovalRequestId,
    Guid UserId,
    string Action,
    string Channel,
    string? Comment) : IRequest<Result>;
