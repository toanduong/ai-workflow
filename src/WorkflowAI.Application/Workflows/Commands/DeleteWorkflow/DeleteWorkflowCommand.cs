using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Workflows.Commands.DeleteWorkflow;

public sealed record DeleteWorkflowCommand(Guid WorkflowId) : IRequest<Result>;
